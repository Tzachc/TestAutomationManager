using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TestAutomationManager.Data;
using TestAutomationManager.Models;
using TestAutomationManager.Services;

namespace TestAutomationManager.Repositories
{
    /// <summary>
    /// Repository for TodoItem and StickyNote CRUD operations
    /// Handles task management with Kanban board and sticky notes functionality
    /// </summary>
    public class TodoRepository
    {
        // ================================================
        // TODO ITEM - CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert a new todo item into the database
        /// </summary>
        public async Task<TodoItem> InsertTodoAsync(TodoItem todo)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    todo.CreatedAt = DateTime.Now;
                    todo.UpdatedAt = DateTime.Now;

                    // Set defaults if not provided
                    if (string.IsNullOrEmpty(todo.Priority))
                        todo.Priority = "Medium";

                    if (string.IsNullOrEmpty(todo.Status))
                        todo.Status = "Todo";

                    if (string.IsNullOrEmpty(todo.CreatedBy))
                        todo.CreatedBy = Environment.UserName;

                    context.TodoItems.Add(todo);
                    await context.SaveChangesAsync();

                    // Log to history
                    await HistoryService.Instance.LogTodoCreatedAsync(todo);

                    return todo;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to insert todo: {ex.Message}", ex);
            }
        }

        // ================================================
        // TODO ITEM - READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get all todo items ordered by status and due date
        /// </summary>
        public async Task<List<TodoItem>> GetAllTodosAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.TodoItems
                        .OrderBy(t => t.IsCompleted)
                        .ThenBy(t => t.SortOrder)
                        .ThenBy(t => t.DueDate)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load todos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a single todo by ID
        /// </summary>
        public async Task<TodoItem?> GetTodoByIdAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load todo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get todos by status (Todo, InProgress, Done)
        /// </summary>
        public async Task<List<TodoItem>> GetTodosByStatusAsync(string status)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.TodoItems
                        .Where(t => t.Status == status)
                        .OrderBy(t => t.SortOrder)
                        .ThenBy(t => t.DueDate)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load todos by status: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get active (non-completed) todos
        /// </summary>
        public async Task<List<TodoItem>> GetActiveTodosAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.TodoItems
                        .Where(t => !t.IsCompleted)
                        .OrderBy(t => t.DueDate)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load active todos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get overdue todos
        /// </summary>
        public async Task<List<TodoItem>> GetOverdueTodosAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var now = DateTime.Now;
                    return await context.TodoItems
                        .Where(t => !t.IsCompleted && t.DueDate.HasValue && t.DueDate.Value < now)
                        .OrderBy(t => t.DueDate)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load overdue todos: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get todos by category
        /// </summary>
        public async Task<List<TodoItem>> GetTodosByCategoryAsync(string category)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.TodoItems
                        .Where(t => t.Category == category)
                        .OrderBy(t => t.IsCompleted)
                        .ThenBy(t => t.DueDate)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load todos by category: {ex.Message}", ex);
            }
        }

        // ================================================
        // TODO ITEM - UPDATE OPERATIONS
        // ================================================

        /// <summary>
        /// Update an existing todo item
        /// </summary>
        public async Task<TodoItem> UpdateTodoAsync(TodoItem todo)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existing = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == todo.Id);
                    if (existing == null)
                        throw new Exception($"Todo with ID {todo.Id} not found");

                    // Update fields
                    existing.Title = todo.Title;
                    existing.Description = todo.Description;
                    existing.DueDate = todo.DueDate;
                    existing.Priority = todo.Priority;
                    existing.Status = todo.Status;
                    existing.Category = todo.Category;
                    existing.Tags = todo.Tags;
                    existing.IsCompleted = todo.IsCompleted;
                    existing.Color = todo.Color;
                    existing.EstimatedMinutes = todo.EstimatedMinutes;
                    existing.ActualMinutes = todo.ActualMinutes;
                    existing.IsRecurring = todo.IsRecurring;
                    existing.RecurrencePattern = todo.RecurrencePattern;
                    existing.AttachmentUrl = todo.AttachmentUrl;
                    existing.SortOrder = todo.SortOrder;
                    existing.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    // Log to history
                    await HistoryService.Instance.LogTodoUpdatedAsync(existing, "Updated");

                    return existing;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update todo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update todo status (for Kanban board drag-drop)
        /// </summary>
        public async Task<bool> UpdateTodoStatusAsync(int id, string newStatus, int sortOrder)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var todo = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
                    if (todo == null) return false;

                    todo.Status = newStatus;
                    todo.SortOrder = sortOrder;
                    todo.UpdatedAt = DateTime.Now;

                    if (newStatus == "Done")
                        todo.IsCompleted = true;

                    await context.SaveChangesAsync();

                    // Log to history
                    await HistoryService.Instance.LogTodoUpdatedAsync(todo, $"Status changed to {newStatus}");

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update todo status: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Toggle todo completion status
        /// </summary>
        public async Task<bool> ToggleTodoCompletionAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var todo = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
                    if (todo == null) return false;

                    todo.IsCompleted = !todo.IsCompleted;
                    todo.Status = todo.IsCompleted ? "Done" : "Todo";
                    todo.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    // Log to history
                    var action = todo.IsCompleted ? "Completed" : "Reopened";
                    await HistoryService.Instance.LogTodoUpdatedAsync(todo, action);

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to toggle todo completion: {ex.Message}", ex);
            }
        }

        // ================================================
        // TODO ITEM - DELETE OPERATIONS
        // ================================================

        /// <summary>
        /// Delete a todo item by ID
        /// </summary>
        public async Task<bool> DeleteTodoAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var todo = await context.TodoItems.FirstOrDefaultAsync(t => t.Id == id);
                    if (todo == null) return false;

                    context.TodoItems.Remove(todo);
                    await context.SaveChangesAsync();

                    // Log to history
                    await HistoryService.Instance.LogTodoDeletedAsync(todo);

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete todo: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Delete all completed todos
        /// </summary>
        public async Task<int> DeleteCompletedTodosAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var completedTodos = await context.TodoItems
                        .Where(t => t.IsCompleted)
                        .ToListAsync();

                    context.TodoItems.RemoveRange(completedTodos);
                    await context.SaveChangesAsync();

                    return completedTodos.Count;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete completed todos: {ex.Message}", ex);
            }
        }

        // ================================================
        // STICKY NOTE - CREATE OPERATIONS
        // ================================================

        /// <summary>
        /// Insert a new sticky note
        /// </summary>
        public async Task<StickyNote> InsertStickyNoteAsync(StickyNote note)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    note.CreatedAt = DateTime.Now;
                    note.UpdatedAt = DateTime.Now;

                    // Set defaults if not provided
                    if (string.IsNullOrEmpty(note.Color))
                        note.Color = "#FFE57F"; // Yellow

                    if (note.Width == 0)
                        note.Width = 200;

                    if (note.Height == 0)
                        note.Height = 200;

                    if (string.IsNullOrEmpty(note.CreatedBy))
                        note.CreatedBy = Environment.UserName;

                    // Set z-index to highest
                    var maxZIndex = await context.StickyNotes.MaxAsync(n => (int?)n.ZIndex) ?? 0;
                    note.ZIndex = maxZIndex + 1;

                    context.StickyNotes.Add(note);
                    await context.SaveChangesAsync();

                    return note;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to insert sticky note: {ex.Message}", ex);
            }
        }

        // ================================================
        // STICKY NOTE - READ OPERATIONS
        // ================================================

        /// <summary>
        /// Get all sticky notes ordered by z-index
        /// </summary>
        public async Task<List<StickyNote>> GetAllStickyNotesAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.StickyNotes
                        .OrderBy(n => n.ZIndex)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load sticky notes: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get a single sticky note by ID
        /// </summary>
        public async Task<StickyNote?> GetStickyNoteByIdAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    return await context.StickyNotes.FirstOrDefaultAsync(n => n.Id == id);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load sticky note: {ex.Message}", ex);
            }
        }

        // ================================================
        // STICKY NOTE - UPDATE OPERATIONS
        // ================================================

        /// <summary>
        /// Update sticky note content and properties
        /// </summary>
        public async Task<StickyNote> UpdateStickyNoteAsync(StickyNote note)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var existing = await context.StickyNotes.FirstOrDefaultAsync(n => n.Id == note.Id);
                    if (existing == null)
                        throw new Exception($"Sticky note with ID {note.Id} not found");

                    // Update fields
                    existing.Content = note.Content;
                    existing.Color = note.Color;
                    existing.PositionX = note.PositionX;
                    existing.PositionY = note.PositionY;
                    existing.Width = note.Width;
                    existing.Height = note.Height;
                    existing.ZIndex = note.ZIndex;
                    existing.IsPinned = note.IsPinned;
                    existing.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();

                    return existing;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update sticky note: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Update sticky note position (for drag-drop)
        /// </summary>
        public async Task<bool> UpdateStickyNotePositionAsync(int id, double x, double y)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var note = await context.StickyNotes.FirstOrDefaultAsync(n => n.Id == id);
                    if (note == null) return false;

                    note.PositionX = x;
                    note.PositionY = y;
                    note.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update sticky note position: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Bring sticky note to front (update z-index)
        /// </summary>
        public async Task<bool> BringStickyNoteToFrontAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var note = await context.StickyNotes.FirstOrDefaultAsync(n => n.Id == id);
                    if (note == null) return false;

                    // Get max z-index and set note to max + 1
                    var maxZIndex = await context.StickyNotes
                        .Where(n => n.Id != id)
                        .MaxAsync(n => (int?)n.ZIndex) ?? 0;

                    note.ZIndex = maxZIndex + 1;
                    note.UpdatedAt = DateTime.Now;

                    await context.SaveChangesAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to bring sticky note to front: {ex.Message}", ex);
            }
        }

        // ================================================
        // STICKY NOTE - DELETE OPERATIONS
        // ================================================

        /// <summary>
        /// Delete a sticky note by ID
        /// </summary>
        public async Task<bool> DeleteStickyNoteAsync(int id)
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var note = await context.StickyNotes.FirstOrDefaultAsync(n => n.Id == id);
                    if (note == null) return false;

                    context.StickyNotes.Remove(note);
                    await context.SaveChangesAsync();

                    return true;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete sticky note: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Delete all sticky notes
        /// </summary>
        public async Task<int> DeleteAllStickyNotesAsync()
        {
            try
            {
                using (var context = new TestAutomationDbContext())
                {
                    var allNotes = await context.StickyNotes.ToListAsync();
                    context.StickyNotes.RemoveRange(allNotes);
                    await context.SaveChangesAsync();

                    return allNotes.Count;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete all sticky notes: {ex.Message}", ex);
            }
        }
    }
}
