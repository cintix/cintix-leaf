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

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

function setLoading(button, loading) {
  if (!button) return;
  if (loading) {
    button.dataset.originalText = button.textContent;
    button.textContent = 'Saving...';
    button.disabled = true;
  } else {
    button.textContent = button.dataset.originalText || button.textContent;
    button.disabled = false;
  }
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

// ── Tabs ──────────────────────────────────────────────

function initWorkspaceTabs() {
  const tabs = document.querySelectorAll('.tab');
  if (!tabs.length) return;
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      const target = tab.dataset.tab;
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('[data-tab-content]').forEach(c => c.hidden = true);
      tab.classList.add('active');
      const content = document.querySelector(`[data-tab-content="${target}"]`);
      if (content) content.hidden = false;
    });
  });
}

// ── Project switcher ──────────────────────────────────

function initProjectSwitcher() {
  const switcher = document.querySelector('#projectSwitcher');
  if (!switcher) return;

  switcher.addEventListener('change', () => {
    if (!switcher.value) return;
    window.location.href = `/dashboard/${encodeURIComponent(switcher.value)}`;
  });
}

// ── Toasts from body data ─────────────────────────────

function initToastsFromBody() {
  const body = document.body;
  if (!body) return;

  const error = body.dataset.toastError;
  const info = body.dataset.toastInfo;

  if (error) showToast(error, 'error');
  if (info) showToast(info, 'info');
}

// ── Modals ────────────────────────────────────────────

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

function appendBacklogRow(item) {
  const tbody = document.querySelector('#backlogTable tbody');
  if (!tbody) return;

  // Remove empty state row if present
  const emptyRow = tbody.querySelector('.empty-state-row');
  if (emptyRow) emptyRow.remove();

  const labels = (item.labelsCsv || '').split(',').filter(x => x.trim()).map(l => {
    const labelObj = (item.labels || []).find(x => x.name.toLowerCase() === l.trim().toLowerCase());
    const color = labelObj ? labelObj.colorHex : '#92A09A';
    return `<span class="label" style="--label-color:${color}">${escapeHtml(l.trim())}</span>`;
  }).join('');

  const statusNum = typeof item.status === 'string' ? statusToInt(item.status) : item.status;
  const projectKey = item.projectKey || '';
  const itemKey = item.key || '';
  const tr = document.createElement('tr');
  tr.className = 'backlog-row';
  tr.draggable = true;
  tr.dataset.itemId = item.id;
  tr.dataset.itemKey = itemKey;
  tr.innerHTML = `
    <td><input type="checkbox" class="bulk-check" value="${item.id}" /></td>
    <td><a href="/projects/${escapeHtml(projectKey)}/${escapeHtml(itemKey)}" class="item-key" onclick="event.stopPropagation()">${escapeHtml(itemKey)}</a> <strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.description || '')}</small></td>
    <td>${escapeHtml(item.type || '')}</td>
    <td><span class="status-tag s-${statusNum}">${escapeHtml(item.status || '')}</span></td>
    <td>${escapeHtml(item.assigneeName || 'Unassigned')}</td>
    <td>${escapeHtml(item.priority || '')}</td>
    <td>${item.storyPoints || 0}</td>
    <td><div class="label-stack">${labels}</div></td>
  `;
  tbody.prepend(tr);

  // Re-bind events
  bindBacklogRowEvents(tr);
  bindWorkItemClicks();
}

function statusToInt(status) {
  const map = { 'ToDo': 1, 'InProgress': 2, 'Review': 3, 'Done': 4 };
  return map[status] || 1;
}

// ── Backlog drag & drop ───────────────────────────────

function initBacklogDnD() {
  const table = document.querySelector('#backlogTable');
  if (!table) return;

  const body = table.querySelector('tbody');
  let dragged = null;
  let wasDragged = false;

  body.querySelectorAll('.backlog-row').forEach(row => {
    bindBacklogRowEvents(row);
  });

  function bindRowEvents(row) {
    row.addEventListener('dragstart', () => {
      dragged = row;
      wasDragged = false;
      row.classList.add('dragging');
    });

    row.addEventListener('dragend', () => {
      row.classList.remove('dragging');
      if (wasDragged) persistOrder();
      dragged = null;
    });

    row.addEventListener('dragover', event => {
      event.preventDefault();
      if (!dragged || dragged === row) return;
      wasDragged = true;

      const rect = row.getBoundingClientRect();
      const shouldInsertAfter = event.clientY > rect.top + rect.height / 2;
      if (shouldInsertAfter) {
        row.after(dragged);
      } else {
        row.before(dragged);
      }
    });
  }

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

      if (!confirm(`Move ${selected.length} item(s) to "${bulkSprintSelect.options[bulkSprintSelect.selectedIndex].text}"?`)) return;

      setLoading(bulkButton, true);
      try {
        await postJson('/api/backlog/move-to-sprint', {
          projectId: Number(table.dataset.projectId),
          sprintId,
          itemIds: selected
        });
        showToast(`Moved ${selected.length} item(s) to sprint`);
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
      } finally {
        setLoading(bulkButton, false);
      }
    });
  }
}

function bindBacklogRowEvents(row) {
  if (!row || row.dataset.bound === '1') return;
  row.dataset.bound = '1';
}

// ── Board drag & drop ─────────────────────────────────

function initBoardDnD() {
  const board = document.querySelector('.board');
  if (!board) return;

  let dragged = null;

  board.querySelectorAll('.board-card').forEach(card => {
    card.addEventListener('dragstart', () => {
      card.dataset.wasDrag = 'false';
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
      dragged.dataset.wasDrag = 'true';
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

// ── Sprint actions ────────────────────────────────────

function initSprintActions() {
  document.querySelectorAll('.sprint-start').forEach(button => {
    button.addEventListener('click', async () => {
      if (!confirm('Start this sprint? Any currently active sprint will be ended.')) return;
      const sprintId = Number(button.dataset.sprintId);
      setLoading(button, true);
      try {
        await postJson(`/api/sprints/${sprintId}/start`, {});
        showToast('Sprint started');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
        setLoading(button, false);
      }
    });
  });

  document.querySelectorAll('.sprint-close').forEach(button => {
    button.addEventListener('click', async () => {
      if (!confirm('Close this sprint? Unfinished items will be moved to the backlog.')) return;
      const sprintId = Number(button.dataset.sprintId);
      setLoading(button, true);
      try {
        await postJson(`/api/sprints/${sprintId}/close`, {
          moveUnfinishedToBacklog: true,
          nextSprintId: null
        });
        showToast('Sprint closed');
        window.location.reload();
      } catch (err) {
        showToast(err.message, 'error');
        setLoading(button, false);
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

      const submitBtn = createForm.querySelector('button[type="submit"]');
      setLoading(submitBtn, true);
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
        setLoading(submitBtn, false);
      }
    });
  }
}

// ── Reports ───────────────────────────────────────────

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
      if (throughput.completedItems === undefined || throughput.completedItems === null) {
        text.textContent = 'No throughput data yet.';
      } else {
        text.textContent = `Completed: ${throughput.completedItems || 0}, Avg points: ${throughput.averageStoryPoints || 0}, Active days: ${throughput.activeDays || 0}`;
      }
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

// ── Live search ───────────────────────────────────────

function initSearch() {
  const searchInput = document.querySelector('input[name="q"]');
  if (!searchInput) return;

  const projectId = document.querySelector('.workspace')?.dataset.projectId;
  if (!projectId) return;

  let debounceTimer;
  searchInput.addEventListener('input', () => {
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(() => performSearch(projectId), 300);
  });

  document.querySelectorAll('.filters select').forEach(sel => {
    sel.addEventListener('change', () => performSearch(projectId));
  });
}

async function performSearch(projectId) {
  const params = new URLSearchParams();
  const q = document.querySelector('input[name="q"]')?.value;
  if (q) params.set('q', q);
  const assignee = document.querySelector('select[name="assigneeId"]')?.value;
  if (assignee) params.set('assigneeId', assignee);
  const sprintId = document.querySelector('select[name="sprintId"]')?.value;
  if (sprintId) params.set('sprintId', sprintId);
  const label = document.querySelector('select[name="label"]')?.value;
  if (label) params.set('label', label);

  try {
    const results = await fetch(`/api/search/${projectId}?${params}`).then(r => r.json());
    updateBacklogTable(results);
  } catch {
    showToast('Search failed', 'error');
  }
}

function updateBacklogTable(items) {
  const tbody = document.querySelector('#backlogTable tbody');
  if (!tbody) return;

  if (items.length === 0) {
    tbody.innerHTML = '<tr class="empty-state-row"><td colspan="8" class="muted" style="text-align:center;padding:2.5rem;">No backlog items match your search.</td></tr>';
    return;
  }

  const projectKey = document.querySelector('.workspace')?.dataset.projectKey || '';
  tbody.innerHTML = items.map(item => {
    const labels = (item.labelsCsv || '').split(',').filter(x => x.trim()).map(l => {
      return `<span class="label" style="--label-color:#92A09A">${escapeHtml(l.trim())}</span>`;
    }).join('');

    const statusNum = typeof item.status === 'string' ? statusToInt(item.status) : item.status;
    const itemKey = item.key || '';
    return `
    <tr class="backlog-row" draggable="true" data-item-id="${item.id}" data-item-key="${escapeHtml(itemKey)}">
      <td><input type="checkbox" class="bulk-check" value="${item.id}" /></td>
      <td><a href="/projects/${escapeHtml(projectKey)}/${escapeHtml(itemKey)}" class="item-key" onclick="event.stopPropagation()">${escapeHtml(itemKey)}</a> <strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.description || '')}</small></td>
      <td>${escapeHtml(item.type || '')}</td>
      <td><span class="status-tag s-${statusNum}">${escapeHtml(item.status || '')}</span></td>
      <td>${escapeHtml(item.assigneeName || 'Unassigned')}</td>
      <td>${escapeHtml(item.priority || '')}</td>
      <td>${item.storyPoints || 0}</td>
      <td><div class="label-stack">${labels}</div></td>
    </tr>`;
  }).join('');

  // Rebind drag events and click handlers
  initBacklogDnD();
  bindWorkItemClicks();
}

// ── Clear filters ─────────────────────────────────────

function initCreateIssueModal() {
  const openBtn = document.querySelector('#openCreateIssueModal');
  const modal = document.querySelector('#createIssueModal');
  if (!openBtn || !modal) return;

  openBtn.addEventListener('click', () => {
    modal.hidden = false;
    modal.querySelector('input[name="Title"]')?.focus();
  });

  const form = document.querySelector('#createIssueForm');
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
      labels: ''
    };

    const submitBtn = form.querySelector('button[type="submit"]');
    setLoading(submitBtn, true);
    try {
      const result = await postJson('/api/workitems', payload);
      showToast('Issue created');
      modal.hidden = true;
      form.reset();
      form.querySelector('input[name="ProjectId"]').value = raw.ProjectId;

      const newItem = await fetch(`/api/workitems/${result.id}`).then(r => r.json());
      appendBacklogRow(newItem);
    } catch (err) {
      showToast(err.message, 'error');
    } finally {
      setLoading(submitBtn, false);
    }
  });
}

function initClearFilters() {
  const btn = document.querySelector('#clearFiltersBtn');
  if (!btn) return;
  btn.addEventListener('click', () => {
    document.querySelector('input[name="q"]').value = '';
    document.querySelectorAll('.filters select').forEach(s => { s.value = ''; });
    const projectId = document.querySelector('.workspace')?.dataset.projectId;
    if (projectId) performSearch(projectId);
  });
}

// ── Work item detail panel (right-side) ─────────────────

function bindWorkItemClicks() {
  document.querySelectorAll('.backlog-row').forEach(row => {
    if (row.dataset.hasClick === '1') return;
    row.dataset.hasClick = '1';
    row.addEventListener('click', event => {
      if (event.target.closest('input[type="checkbox"]')) return;
      if (event.target.closest('a.item-key')) return;
      openDetailPanel(Number(row.dataset.itemId));
    });
  });

  document.querySelectorAll('.board-card').forEach(card => {
    if (card.dataset.hasClick === '1') return;
    card.dataset.hasClick = '1';
    card.addEventListener('click', event => {
      if (event.target.closest('a.item-key')) return;
      if (card.dataset.wasDrag === 'true') {
        card.dataset.wasDrag = 'false';
        return;
      }
      openDetailPanel(Number(card.dataset.itemId));
    });
  });
}

async function openDetailPanel(itemId) {
  const panel = document.querySelector('#detailPanel');
  const overlay = document.querySelector('#detailPanelOverlay');
  if (!panel || !overlay) return;
  panel.hidden = false;
  overlay.hidden = false;

  const body = document.querySelector('#detailPanelBody');
  const title = document.querySelector('#detailPanelTitle');
  body.innerHTML = '<p class="muted">Loading...</p>';
  if (title) title.textContent = 'Loading...';

  try {
    const data = await fetch(`/api/workitems/${itemId}`).then(r => {
      if (!r.ok) throw new Error('Failed to load work item');
      return r.json();
    });
    if (title) title.textContent = data.key;
    renderDetailPanel(data, body);
  } catch (err) {
    body.innerHTML = `<p class="inline-error">${err.message}</p>`;
  }
}

function closeDetailPanel() {
  document.querySelector('#detailPanel').hidden = true;
  document.querySelector('#detailPanelOverlay').hidden = true;
}

function autoSaveField(itemId, field, value) {
  const payload = {
    title: '', description: '', type: 2, status: 1, priority: 2,
    assigneeUserId: null, storyPoints: 0, labels: '', dueDate: null
  };
  if (field === 'title') payload.title = value;
  if (field === 'description') payload.description = value;
  if (field === 'type') { payload.type = Number(value); payload.status = 1; payload.priority = 2; }
  if (field === 'status') payload.status = Number(value);
  if (field === 'priority') payload.priority = Number(value);
  if (field === 'assigneeUserId') payload.assigneeUserId = value ? Number(value) : null;
  if (field === 'storyPoints') payload.storyPoints = Number(value) || 0;
  if (field === 'dueDate') payload.dueDate = value || null;

  return fetch(`/api/workitems/${itemId}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
}

function renderDetailPanel(data, container) {
  const typeOptions = ['Epic', 'Story', 'Task', 'Bug', 'Subtask']
    .map((t, i) => `<option value="${i + 1}" ${data.type === t ? 'selected' : ''}>${t}</option>`).join('');

  const statusOptions = ['ToDo', 'InProgress', 'Review', 'Done']
    .map((s, i) => `<option value="${i + 1}" ${data.status === s ? 'selected' : ''}>${s.replace(/([A-Z])/g, ' $1').trim()}</option>`).join('');

  const priorityOptions = ['Low', 'Medium', 'High', 'Critical']
    .map((p, i) => `<option value="${i + 1}" ${data.priority === p ? 'selected' : ''}>${p}</option>`).join('');

  const userOptions = data.users.map(u =>
    `<option value="${u.id}" ${u.id === data.assigneeUserId ? 'selected' : ''}>${escapeHtml(u.displayName)}</option>`
  ).join('');

  const labelTags = data.labelsCsv
    ? data.labelsCsv.split(',').filter(x => x.trim()).map(l => {
        const labelObj = (data.labels || []).find(x => x.name.toLowerCase() === l.trim().toLowerCase());
        const color = labelObj ? labelObj.colorHex : '#92A09A';
        return `<span class="label" style="--label-color:${color}">${escapeHtml(l.trim())}</span>`;
      }).join('')
    : '<span class="muted">No labels</span>';

  container.innerHTML = `
    <a href="/projects/${escapeHtml(data.projectKey)}/${escapeHtml(data.key)}" class="leaf-button ghost small" style="align-self:flex-start;">Open full page</a>

    <div class="detail-field">
      <label>Title</label>
      <div class="editable" data-field="title" data-id="${data.id}">${escapeHtml(data.title)}</div>
    </div>
    <div class="detail-field">
      <label>Description</label>
      <div class="editable" data-field="description" data-id="${data.id}" data-type="textarea">${escapeHtml(data.description || 'No description — click to add')}</div>
    </div>
    <div class="inline-grid" style="grid-template-columns: 1fr 1fr 1fr;">
      <div class="detail-field">
        <label>Type</label>
        <select class="leaf-input auto-save-panel" data-field="type" data-id="${data.id}">${typeOptions}</select>
      </div>
      <div class="detail-field">
        <label>Status</label>
        <select class="leaf-input auto-save-panel" data-field="status" data-id="${data.id}">${statusOptions}</select>
      </div>
      <div class="detail-field">
        <label>Priority</label>
        <select class="leaf-input auto-save-panel" data-field="priority" data-id="${data.id}">${priorityOptions}</select>
      </div>
      <div class="detail-field">
        <label>Assignee</label>
        <select class="leaf-input auto-save-panel" data-field="assigneeUserId" data-id="${data.id}">
          <option value="">Unassigned</option>
          ${userOptions}
        </select>
      </div>
      <div class="detail-field">
        <label>Story Points</label>
        <input class="leaf-input auto-save-panel" type="number" min="0" data-field="storyPoints" data-id="${data.id}" value="${data.storyPoints || 0}" />
      </div>
      <div class="detail-field">
        <label>Due Date</label>
        <input class="leaf-input auto-save-panel" type="date" data-field="dueDate" data-id="${data.id}" value="${data.dueDate || ''}" />
      </div>
    </div>
    <div class="detail-field">
      <label>Labels</label>
      <div class="label-stack">${labelTags}</div>
    </div>
    <small class="muted">Reporter: ${escapeHtml(data.reporterName || 'Unknown')} · Created: ${new Date(data.createdUtc).toLocaleDateString()}</small>

    <div class="detail-section">
      <h4>Attachments (${(data.attachments || []).length})</h4>
      <div id="panel-attachments-list">
        ${(data.attachments || []).length === 0 ? '<p class="muted">No attachments yet.</p>' :
          (data.attachments || []).map(a => `
            <div class="comment-item" style="display:flex;justify-content:space-between;align-items:center;">
              <div>
                <a href="/api/attachments/${a.id}" download="${escapeHtml(a.fileName)}" style="color:var(--accent);font-weight:600;">${escapeHtml(a.fileName)}</a>
                <small>${formatFileSize(a.fileSize)} · ${escapeHtml(a.uploadedByName)}</small>
              </div>
              <button class="leaf-button ghost small panel-attachment-delete" data-attachment-id="${a.id}" style="color:var(--danger);">Delete</button>
            </div>`).join('')}
      </div>
      <div style="margin-top:var(--space-2);">
        <input type="file" id="panel-file-input" hidden />
        <button class="leaf-button ghost small" id="panel-upload-btn">Attach file</button>
      </div>
    </div>

    <div class="detail-actions">
      <button class="leaf-button ghost" id="panel-delete-btn" style="color:var(--danger);">Delete</button>
    </div>

    <div class="detail-section">
      <h4>Comments (${data.comments.length})</h4>
      <div id="panel-comments-list">
        ${data.comments.length === 0 ? '<p class="muted">No comments yet.</p>' :
          data.comments.map(c => `
            <div class="comment-item">
              <strong>${escapeHtml(c.authorName)}</strong>
              <small>${new Date(c.createdUtc).toLocaleString()}</small>
              <p>${escapeHtml(c.body)}</p>
            </div>`).join('')}
      </div>
      <div class="quick-comment">
        <input class="leaf-input" id="panel-comment-input" placeholder="Add a comment..." />
        <button class="leaf-button ghost small" id="panel-comment-btn">Comment</button>
      </div>
    </div>

    <div class="detail-section">
      <h4>Activity</h4>
      ${data.activity.length === 0 ? '<p class="muted">No activity yet.</p>' :
        `<ul class="activity-list">${data.activity.map(a => `
          <li>
            <span>${escapeHtml(a.description)}</span>
            <small>${new Date(a.createdUtc).toLocaleString()}</small>
          </li>`).join('')}</ul>`}
    </div>
  `;

  // Click-to-edit for text fields
  container.querySelectorAll('.editable').forEach(el => {
    el.addEventListener('click', () => {
      if (el.querySelector('input, textarea')) return;
      const field = el.dataset.field;
      const id = Number(el.dataset.id);
      const isTextarea = el.dataset.type === 'textarea';
      const current = el.textContent.trim();
      const input = document.createElement(isTextarea ? 'textarea' : 'input');
      input.className = 'leaf-input';
      if (isTextarea) input.rows = 4;
      input.value = current === 'No description — click to add' ? '' : current;
      el.innerHTML = '';
      el.appendChild(input);
      input.focus();
      const save = async () => {
        const val = input.value;
        el.textContent = val || (isTextarea ? 'No description — click to add' : (field === 'title' ? '(untitled)' : ''));
        try { await autoSaveField(id, field, val); showToast('Saved'); } catch {}
      };
      input.addEventListener('blur', save);
      input.addEventListener('keydown', e => { if (e.key === 'Enter' && !isTextarea) { e.preventDefault(); save(); } });
    });
  });

  // Auto-save for selects/inputs
  container.querySelectorAll('.auto-save-panel').forEach(el => {
    let timer;
    el.addEventListener('change', async () => {
      clearTimeout(timer);
      const id = Number(el.dataset.id);
      const field = el.dataset.field;
      const val = el.type === 'number' ? Number(el.value) : (el.value || null);
      try { await autoSaveField(id, field, val); showToast('Saved'); } catch {}
    });
  });

  // Delete
  const deleteBtn = container.querySelector('#panel-delete-btn');
  deleteBtn.addEventListener('click', async () => {
    if (!confirm('Delete this work item? This cannot be undone.')) return;
    setLoading(deleteBtn, true);
    try {
      await fetch(`/api/workitems/${data.id}`, { method: 'DELETE' });
      showToast('Work item deleted');
      closeDetailPanel();
      window.location.reload();
    } catch (err) {
      showToast(err.message, 'error');
      setLoading(deleteBtn, false);
    }
  });

  // Comment
  const commentBtn = container.querySelector('#panel-comment-btn');
  const commentInput = container.querySelector('#panel-comment-input');
  commentBtn.addEventListener('click', async () => {
    const body = commentInput.value.trim();
    if (!body) return;
    setLoading(commentBtn, true);
    try {
      await postJson('/api/workitems/comment', { workItemId: data.id, body });
      commentInput.value = '';
      const fresh = await fetch(`/api/workitems/${data.id}`).then(r => r.json());
      renderDetailPanel(fresh, container);
    } catch (err) {
      showToast(err.message, 'error');
    } finally {
      setLoading(commentBtn, false);
    }
  });
  commentInput.addEventListener('keydown', e => { if (e.key === 'Enter') commentBtn.click(); });

  // Attachment upload
  const uploadBtn = container.querySelector('#panel-upload-btn');
  const fileInput = container.querySelector('#panel-file-input');
  if (uploadBtn && fileInput) {
    uploadBtn.addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', async () => {
      const file = fileInput.files[0];
      if (!file) return;
      const formData = new FormData();
      formData.append('file', file);
      setLoading(uploadBtn, true);
      try {
        const resp = await fetch(`/api/workitems/${data.id}/attachments`, { method: 'POST', body: formData });
        if (!resp.ok) { const err = await resp.json().catch(() => ({})); throw new Error(err.error || 'Upload failed'); }
        showToast(`Uploaded: ${file.name}`);
        const fresh = await fetch(`/api/workitems/${data.id}`).then(r => r.json());
        renderDetailPanel(fresh, container);
      } catch (err) {
        showToast(err.message, 'error');
      } finally {
        setLoading(uploadBtn, false);
        fileInput.value = '';
      }
    });
  }

  // Attachment delete
  container.querySelectorAll('.panel-attachment-delete').forEach(btn => {
    btn.addEventListener('click', async () => {
      if (!confirm('Delete this attachment?')) return;
      const attId = Number(btn.dataset.attachmentId);
      setLoading(btn, true);
      try {
        await fetch(`/api/attachments/${attId}`, { method: 'DELETE' });
        showToast('Attachment deleted');
        const fresh = await fetch(`/api/workitems/${data.id}`).then(r => r.json());
        renderDetailPanel(fresh, container);
      } catch (err) {
        showToast(err.message, 'error');
      } finally {
        setLoading(btn, false);
      }
    });
  });
}

function formatFileSize(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
}

function initDetailPanel() {
  const closeBtn = document.querySelector('#closeDetailPanel');
  const overlay = document.querySelector('#detailPanelOverlay');
  if (closeBtn) closeBtn.addEventListener('click', closeDetailPanel);
  if (overlay) overlay.addEventListener('click', closeDetailPanel);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape') closeDetailPanel();
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

// ── Init ──────────────────────────────────────────────

initProjectSwitcher();
initToastsFromBody();
initModals();
initWorkspaceTabs();
initDetailPanel();
initCreateIssueModal();
initBacklogDnD();
initBoardDnD();
initSprintActions();
initSearch();
initClearFilters();
bindWorkItemClicks();
loadReports();
initKeyboardSubmit();
