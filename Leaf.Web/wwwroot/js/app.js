const toastContainer = document.querySelector('#toastContainer');

function showToast(message, type = 'info') {
  if (!message) return;
  if (!toastContainer) return;

  const node = document.createElement('div');
  node.className = `toast ${type === 'error' ? 'error' : ''}`;
  node.textContent = message;
  toastContainer.append(node);
  setTimeout(() => node.remove(), 3200);
}

async function postJson(url, payload) {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });

  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    throw new Error(data.error || `Request failed (${response.status})`);
  }

  return response.json().catch(() => ({}));
}

function parseForm(form) {
  const data = new FormData(form);
  const obj = {};
  for (const [key, value] of data.entries()) {
    obj[key] = value;
  }
  return obj;
}

function initProjectSwitcher() {
  const switcher = document.querySelector('#projectSwitcher');
  if (!switcher) return;

  switcher.addEventListener('change', () => {
    if (!switcher.value) return;
    window.location.href = `/dashboard/${switcher.value}`;
  });
}

function initToastsFromBody() {
  const body = document.body;
  if (!body) return;

  const error = body.dataset.toastError;
  const info = body.dataset.toastInfo;

  if (error) showToast(error, 'error');
  if (info) showToast(info, 'info');
}

function initModals() {
  document.querySelectorAll('[data-close-modal]').forEach(button => {
    button.addEventListener('click', () => {
      const selector = button.getAttribute('data-close-modal');
      const modal = document.querySelector(selector);
      if (modal) modal.hidden = true;
    });
  });

  const openSprint = document.querySelector('#openSprintModal');
  if (openSprint) {
    openSprint.addEventListener('click', () => {
      const modal = document.querySelector('#sprintModal');
      if (modal) {
        modal.hidden = false;
        modal.querySelector('input,textarea,select')?.focus();
      }
    });
  }

  document.addEventListener('keydown', event => {
    if (event.key !== 'Escape') return;
    document.querySelectorAll('.modal').forEach(modal => {
      modal.hidden = true;
    });
  });
}

function initQuickCreate() {
  const form = document.querySelector('#quickCreateForm');
  if (!form) return;

  form.addEventListener('submit', async event => {
    event.preventDefault();
    const raw = parseForm(form);

    const payload = {
      projectId: Number(raw.ProjectId),
      title: raw.Title,
      description: raw.Description || '',
      type: Number(raw.Type || 2),
      priority: Number(raw.Priority || 2),
      assigneeUserId: raw.AssigneeUserId ? Number(raw.AssigneeUserId) : null,
      storyPoints: Number(raw.StoryPoints || 0),
      labels: raw.Labels || ''
    };

    try {
      await postJson('/api/workitems', payload);
      showToast('Work item created');
      window.location.reload();
    } catch (err) {
      showToast(err.message, 'error');
    }
  });
}

function initBacklogDnD() {
  const table = document.querySelector('#backlogTable');
  if (!table) return;

  const body = table.querySelector('tbody');
  let dragged = null;

  body.querySelectorAll('.backlog-row').forEach(row => {
    row.addEventListener('dragstart', () => {
      dragged = row;
      row.classList.add('dragging');
    });

    row.addEventListener('dragend', () => {
      row.classList.remove('dragging');
      dragged = null;
      persistOrder();
    });

    row.addEventListener('dragover', event => {
      event.preventDefault();
      if (!dragged || dragged === row) return;

      const rect = row.getBoundingClientRect();
      const shouldInsertAfter = event.clientY > rect.top + rect.height / 2;
      if (shouldInsertAfter) {
        row.after(dragged);
      } else {
        row.before(dragged);
      }
    });
  });

  async function persistOrder() {
    const ordered = [...body.querySelectorAll('.backlog-row')].map(row => Number(row.dataset.itemId));

    try {
      await postJson('/api/workitems/reorder', {
        projectId: Number(table.dataset.projectId),
        orderedIds: ordered
      });
    } catch (err) {
      showToast(err.message, 'error');
    }
  }

  const bulkButton = document.querySelector('#bulkMoveBtn');
  const bulkSprintSelect = document.querySelector('#bulkSprintSelect');

  if (bulkButton && bulkSprintSelect) {
    bulkButton.addEventListener('click', async () => {
      const sprintId = Number(bulkSprintSelect.value || 0);
      if (!sprintId) {
        showToast('Select a sprint first', 'error');
        return;
      }

      const selected = [...document.querySelectorAll('.bulk-check:checked')].map(x => Number(x.value));
      if (selected.length === 0) {
        showToast('Select at least one backlog item', 'error');
        return;
      }

      try {
        await postJson('/api/backlog/move-to-sprint', {
          projectId: Number(table.dataset.projectId),
          sprintId,
          itemIds: selected
        });
        showToast('Moved selected items to sprint');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
}

function initBoardDnD() {
  const board = document.querySelector('.board');
  if (!board) return;

  let dragged = null;

  board.querySelectorAll('.board-card').forEach(card => {
    card.addEventListener('dragstart', () => {
      dragged = card;
      card.classList.add('dragging');
    });

    card.addEventListener('dragend', () => {
      card.classList.remove('dragging');
      dragged = null;
    });
  });

  board.querySelectorAll('.board-dropzone').forEach(zone => {
    zone.addEventListener('dragover', event => {
      event.preventDefault();
      zone.classList.add('over');
      autoScrollBoard(event, board);

      if (!dragged) return;
      const afterElement = [...zone.querySelectorAll('.board-card:not(.dragging)')].find(item => {
        const rect = item.getBoundingClientRect();
        return event.clientY <= rect.top + rect.height / 2;
      });

      if (afterElement) {
        zone.insertBefore(dragged, afterElement);
      } else {
        zone.appendChild(dragged);
      }
    });

    zone.addEventListener('dragleave', () => zone.classList.remove('over'));

    zone.addEventListener('drop', async () => {
      zone.classList.remove('over');
      if (!dragged) return;

      const column = zone.closest('.board-column');
      const status = Number(column?.dataset.status || 1);
      const cards = [...zone.querySelectorAll('.board-card')];
      const order = cards.findIndex(x => x === dragged);

      try {
        await postJson('/api/workitems/move', {
          workItemId: Number(dragged.dataset.itemId),
          status,
          columnOrder: order < 0 ? 0 : order,
          sprintId: Number(board.dataset.sprintId || 0) || null
        });
        showToast('Board updated');
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  });
}

function autoScrollBoard(event, board) {
  const threshold = 90;
  const rect = board.getBoundingClientRect();
  if (event.clientX > rect.right - threshold) {
    board.scrollBy({ left: 12, behavior: 'auto' });
  }
  if (event.clientX < rect.left + threshold) {
    board.scrollBy({ left: -12, behavior: 'auto' });
  }
}

function initSprintActions() {
  document.querySelectorAll('.sprint-start').forEach(button => {
    button.addEventListener('click', async () => {
      const sprintId = Number(button.dataset.sprintId);
      try {
        await postJson(`/api/sprints/${sprintId}/start`, {});
        showToast('Sprint started');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  });

  document.querySelectorAll('.sprint-close').forEach(button => {
    button.addEventListener('click', async () => {
      const sprintId = Number(button.dataset.sprintId);
      try {
        await postJson(`/api/sprints/${sprintId}/close`, {
          moveUnfinishedToBacklog: true,
          nextSprintId: null
        });
        showToast('Sprint closed');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  });

  const createForm = document.querySelector('#createSprintForm');
  if (createForm) {
    createForm.addEventListener('submit', async event => {
      event.preventDefault();
      const raw = parseForm(createForm);

      const ids = (raw.WorkItemIds || '')
        .split(',')
        .map(x => Number(x.trim()))
        .filter(x => Number.isFinite(x) && x > 0);

      try {
        await postJson('/api/sprints', {
          projectId: Number(raw.ProjectId),
          name: raw.Name,
          goal: raw.Goal || '',
          capacityNote: raw.CapacityNote || '',
          startDate: raw.StartDate,
          endDate: raw.EndDate,
          workItemIds: ids
        });
        showToast('Sprint created');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
}

async function loadReports() {
  const panel = document.querySelector('#reportsPanel');
  if (!panel) return;

  try {
    const data = await fetch(`/api/reports/${panel.dataset.projectId}`).then(x => x.json());
    renderBurndown(data.burndown || []);
    renderVelocity(data.velocity || []);

    const throughput = data.throughput || {};
    const text = document.querySelector('#throughputText');
    if (text) {
      text.textContent = `Completed: ${throughput.completedItems || 0}, Avg points: ${throughput.averageStoryPoints || 0}, Active days: ${throughput.activeDays || 0}`;
    }
  } catch {
    showToast('Failed to load reports', 'error');
  }
}

function renderBurndown(points) {
  const chart = document.querySelector('#burndownChart');
  if (!chart) return;

  chart.innerHTML = '';
  if (points.length === 0) {
    chart.innerHTML = '<p class="muted">No burndown points yet.</p>';
    return;
  }

  const max = Math.max(...points.map(x => x.remainingPoints || x.remaining));
  points.forEach(point => {
    const value = point.remainingPoints ?? point.remaining;
    const col = document.createElement('div');
    col.className = 'chart-col';
    col.style.height = `${Math.max(8, (value / Math.max(1, max)) * 100)}px`;
    col.dataset.label = String(point.day).slice(5);
    chart.append(col);
  });
}

function renderVelocity(points) {
  const chart = document.querySelector('#velocityChart');
  if (!chart) return;

  chart.innerHTML = '';
  if (points.length === 0) {
    chart.innerHTML = '<p class="muted">No closed sprints available.</p>';
    return;
  }

  const max = Math.max(...points.flatMap(x => [x.planned, x.completed]));

  points.forEach(point => {
    const planned = document.createElement('div');
    planned.className = 'chart-col secondary';
    planned.style.height = `${Math.max(8, (point.planned / Math.max(1, max)) * 100)}px`;
    planned.dataset.label = `${point.sprintName}-P`;

    const completed = document.createElement('div');
    completed.className = 'chart-col';
    completed.style.height = `${Math.max(8, (point.completed / Math.max(1, max)) * 100)}px`;
    completed.dataset.label = `${point.sprintName}-C`;

    chart.append(planned, completed);
  });
}

function initKeyboardSubmit() {
  document.querySelectorAll('.modal input').forEach(input => {
    input.addEventListener('keydown', event => {
      if (event.key !== 'Enter') return;
      const form = input.closest('form');
      if (!form) return;
      event.preventDefault();
      form.requestSubmit();
    });
  });
}

initProjectSwitcher();
initToastsFromBody();
initModals();
initQuickCreate();
initBacklogDnD();
initBoardDnD();
initSprintActions();
loadReports();
initKeyboardSubmit();
