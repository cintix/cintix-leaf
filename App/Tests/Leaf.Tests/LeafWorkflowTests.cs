using Leaf.Features.Shared.Contracts;
using Leaf.Features.Shared.Models;
using Leaf.Tests.Support;

namespace Leaf.Tests;

public sealed class LeafWorkflowTests
{
    [Fact]
    public async Task Auth_Login_Succeeds_WithSeededAdminCredentials()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();

        // Act
        var result = await ctx.AuthService.LoginAsync("admin@leaf.local", "leafadmin");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Admin", result.Value.DisplayName);
    }

    [Fact]
    public async Task Auth_Login_Fails_WithInvalidPassword()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();

        // Act
        var result = await ctx.AuthService.LoginAsync("admin@leaf.local", "wrong-password");

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("unauthorized", result.Error.Code);
    }

    [Fact]
    public async Task Backlog_Reorder_UpdatesRankingOrder()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        var backlog = await ctx.WorkItemService.GetBacklogAsync(new WorkItemSearchFilters(1, null, null, null, null, null, null));
        var reversedIds = backlog.Select(x => x.Id).Reverse().ToArray();

        // Act
        await ctx.WorkItemService.ReorderBacklogAsync(1, reversedIds);
        var reordered = await ctx.WorkItemService.GetBacklogAsync(new WorkItemSearchFilters(1, null, null, null, null, null, null));

        // Assert
        Assert.Equal(reversedIds, reordered.Select(x => x.Id));
    }

    [Fact]
    public async Task Search_FilterByAssigneeAndLabel_ReturnsExpectedItems()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        await ctx.CreateWorkItemAsync("Assignee target", 3, assigneeUserId: 2, labelsCsv: "target,backend");
        await ctx.CreateWorkItemAsync("Other item", 3, assigneeUserId: 3, labelsCsv: "other");

        // Act
        var result = await ctx.SearchService.SearchAsync(new WorkItemSearchFilters(
            ProjectId: 1,
            Query: "Assignee",
            AssigneeUserId: 2,
            SprintId: null,
            Status: null,
            Type: null,
            Labels: ["target"]));

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result[0].AssigneeUserId);
        Assert.Contains("target", result[0].LabelsCsv);
    }

    [Fact]
    public async Task IssueTransition_Move_UpdatesStatusAndColumnOrder()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        var workItemId = await ctx.CreateWorkItemAsync("Transition me", 5, assigneeUserId: 2);

        // Act
        var moveResult = await ctx.WorkItemService.MoveAsync(workItemId, WorkItemStatus.InProgress, 4, null, actorUserId: 1);
        var updated = await ctx.Repository.GetWorkItemAsync(workItemId);

        // Assert
        Assert.True(moveResult.IsSuccess);
        Assert.NotNull(updated);
        Assert.Equal(WorkItemStatus.InProgress, updated!.Status);
        Assert.Equal(4, updated.ColumnOrder);
    }

    [Fact]
    public async Task Sprint_StoryPointTotals_MatchPlannedItems()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        var a = await ctx.CreateWorkItemAsync("SP-A", 3);
        var b = await ctx.CreateWorkItemAsync("SP-B", 5);

        var create = await ctx.SprintService.CreateSprintAsync(new CreateSprintInput(
            ProjectId: 1,
            Name: "Planning Sprint",
            Goal: "Verify totals",
            CapacityNote: "8 points",
            StartDate: new DateOnly(2026, 04, 20),
            EndDate: new DateOnly(2026, 05, 03),
            WorkItemIds: [a, b]));

        // Act
        var totals = await ctx.SprintService.GetStoryPointTotalsAsync(create.Value);

        // Assert
        Assert.True(create.IsSuccess);
        Assert.Equal(8, totals.planned);
        Assert.Equal(0, totals.completed);
    }

    [Fact]
    public async Task Sprint_Close_MovesUnfinishedItemsToBacklog_WhenConfigured()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        var doneItem = await ctx.CreateWorkItemAsync("Done in sprint", 3);
        var carryItem = await ctx.CreateWorkItemAsync("Carry this", 5);

        var create = await ctx.SprintService.CreateSprintAsync(new CreateSprintInput(
            ProjectId: 1,
            Name: "Close Sprint",
            Goal: "Carry-over check",
            CapacityNote: "8 points",
            StartDate: new DateOnly(2026, 04, 20),
            EndDate: new DateOnly(2026, 05, 03),
            WorkItemIds: [doneItem, carryItem]));

        await ctx.SprintService.StartSprintAsync(create.Value, actorUserId: 1);
        await ctx.WorkItemService.MoveAsync(doneItem, WorkItemStatus.Done, 0, create.Value, actorUserId: 1);
        await ctx.WorkItemService.MoveAsync(carryItem, WorkItemStatus.InProgress, 0, create.Value, actorUserId: 1);

        // Act
        var close = await ctx.SprintService.CloseSprintAsync(new CloseSprintInput(create.Value, MoveUnfinishedToBacklog: true, NextSprintId: null), actorUserId: 1);
        var done = await ctx.Repository.GetWorkItemAsync(doneItem);
        var carry = await ctx.Repository.GetWorkItemAsync(carryItem);

        // Assert
        Assert.True(close.IsSuccess);
        Assert.NotNull(done);
        Assert.NotNull(carry);
        Assert.Equal(create.Value, done!.SprintId);
        Assert.Null(carry!.SprintId);
    }

    [Fact]
    public async Task Reports_BurndownAndVelocity_ReturnData_AfterSprintLifecycle()
    {
        // Arrange
        await using var ctx = await IntegrationTestContext.CreateAsync();
        var a = await ctx.CreateWorkItemAsync("Report-A", 3);
        var b = await ctx.CreateWorkItemAsync("Report-B", 2);

        var create = await ctx.SprintService.CreateSprintAsync(new CreateSprintInput(
            ProjectId: 1,
            Name: "Report Sprint",
            Goal: "Report data",
            CapacityNote: "5 points",
            StartDate: new DateOnly(2026, 04, 20),
            EndDate: new DateOnly(2026, 05, 03),
            WorkItemIds: [a, b]));

        await ctx.SprintService.StartSprintAsync(create.Value, actorUserId: 1);
        await ctx.WorkItemService.MoveAsync(a, WorkItemStatus.Done, 0, create.Value, actorUserId: 1);
        await ctx.SprintService.CloseSprintAsync(new CloseSprintInput(create.Value, MoveUnfinishedToBacklog: true, NextSprintId: null), actorUserId: 1);

        // Act
        var burndown = await ctx.ReportService.GetBurndownAsync(create.Value);
        var velocity = await ctx.ReportService.GetVelocityAsync(1);

        // Assert
        Assert.NotEmpty(burndown);
        Assert.NotEmpty(velocity);
        Assert.Contains(velocity, x => x.SprintName == "Report Sprint" && x.Completed >= 3);
    }
}
