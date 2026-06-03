using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/runs/{runId:guid}")]
public sealed class RunSocialController(HouseOfRunsDbContext db) : ControllerBase
{
    [HttpPost("likes")]
    public async Task<ActionResult<RunSocialSummaryResponse>> Like(Guid runId)
    {
        var currentUserId = User.GetRequiredUserId();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var like = await db.RunLikes.FirstOrDefaultAsync(candidate =>
            candidate.RunId == runId && candidate.UserId == currentUserId);

        if (like is null)
        {
            db.RunLikes.Add(new RunLike { RunId = runId, UserId = currentUserId });
        }
        else if (!like.IsActive)
        {
            like.IsActive = true;
            like.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return Ok(await BuildSummaryAsync(runId, currentUserId));
    }

    [HttpDelete("likes")]
    public async Task<IActionResult> Unlike(Guid runId)
    {
        var currentUserId = User.GetRequiredUserId();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var like = await db.RunLikes.FirstOrDefaultAsync(candidate =>
            candidate.RunId == runId && candidate.UserId == currentUserId);

        if (like is not null && like.IsActive)
        {
            like.IsActive = false;
            like.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpGet("comments")]
    public async Task<ActionResult<PagedResponse<RunCommentResponse>>> GetComments(
        Guid runId,
        [FromQuery] string? q,
        [FromQuery] string? userName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = "createdAt",
        [FromQuery] string? sortDir = "desc")
    {
        page = Paging.NormalizePage(page);
        pageSize = Paging.NormalizePageSize(pageSize);
        var currentUserId = User.GetRequiredUserId();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var query = db.RunComments
            .AsNoTracking()
            .Include(comment => comment.User)
            .Where(comment => comment.RunId == runId && !comment.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(comment => comment.Body.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            var term = userName.Trim().ToLower();
            query = query.Where(comment =>
                comment.User != null &&
                (comment.User.UserName.ToLower().Contains(term) || comment.User.DisplayName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync();
        query = SortComments(query, sortBy, sortDir);

        var comments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResponse<RunCommentResponse>(comments.Select(ToResponse).ToList(), page, pageSize, total));
    }

    [HttpPost("comments")]
    public async Task<ActionResult<RunCommentResponse>> CreateComment(Guid runId, RunCommentRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid comment",
                Detail = "Comment text is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var comment = new RunComment
        {
            RunId = runId,
            UserId = currentUserId,
            Body = body
        };

        db.RunComments.Add(comment);
        await db.SaveChangesAsync();

        var created = await db.RunComments
            .AsNoTracking()
            .Include(candidate => candidate.User)
            .FirstAsync(candidate => candidate.Id == comment.Id);

        return CreatedAtAction(nameof(GetComments), new { runId }, ToResponse(created));
    }

    [HttpPut("comments/{commentId:guid}")]
    public async Task<ActionResult<RunCommentResponse>> UpdateComment(Guid runId, Guid commentId, RunCommentRequest request)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var comment = await db.RunComments
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.Id == commentId && candidate.RunId == runId && !candidate.IsDeleted);

        if (comment is null)
        {
            return NotFound();
        }

        if (!isAdmin && comment.UserId != currentUserId && run.UserId != currentUserId)
        {
            return Forbid();
        }

        var body = request.Body.Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid comment",
                Detail = "Comment text is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        comment.Body = body;
        comment.IsEdited = true;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToResponse(comment));
    }

    [HttpDelete("comments/{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid runId, Guid commentId)
    {
        var currentUserId = User.GetRequiredUserId();
        var isAdmin = User.IsAdmin();
        var run = await GetVisibleRunAsync(runId, currentUserId);
        if (run is null)
        {
            return NotFound();
        }

        var comment = await db.RunComments.FirstOrDefaultAsync(candidate =>
            candidate.Id == commentId && candidate.RunId == runId && !candidate.IsDeleted);

        if (comment is null)
        {
            return NotFound();
        }

        if (!isAdmin && comment.UserId != currentUserId)
        {
            return Forbid();
        }

        comment.IsDeleted = true;
        comment.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Run?> GetVisibleRunAsync(Guid runId, Guid currentUserId)
    {
        var run = await db.Runs.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == runId);
        if (run is null)
        {
            return null;
        }

        return User.IsAdmin() || run.IsPublic || run.UserId == currentUserId ? run : null;
    }

    private async Task<RunSocialSummaryResponse> BuildSummaryAsync(Guid runId, Guid currentUserId)
    {
        var likeCount = await db.RunLikes.CountAsync(like => like.RunId == runId && like.IsActive);
        var commentCount = await db.RunComments.CountAsync(comment => comment.RunId == runId && !comment.IsDeleted);
        var hasLiked = await db.RunLikes.AnyAsync(like => like.RunId == runId && like.UserId == currentUserId && like.IsActive);
        return new RunSocialSummaryResponse(runId, likeCount, commentCount, hasLiked);
    }

    private static IQueryable<RunComment> SortComments(IQueryable<RunComment> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy ?? "createdAt").ToLowerInvariant() switch
        {
            "username" => desc
                ? query.OrderByDescending(comment => comment.User != null ? comment.User.UserName : string.Empty)
                : query.OrderBy(comment => comment.User != null ? comment.User.UserName : string.Empty),
            "updatedat" => desc ? query.OrderByDescending(comment => comment.UpdatedAt) : query.OrderBy(comment => comment.UpdatedAt),
            _ => desc ? query.OrderByDescending(comment => comment.CreatedAt) : query.OrderBy(comment => comment.CreatedAt)
        };
    }

    private static RunCommentResponse ToResponse(RunComment comment) => new(
        comment.Id,
        comment.RunId,
        comment.UserId,
        comment.User?.UserName ?? string.Empty,
        comment.User?.DisplayName ?? string.Empty,
        comment.Body,
        comment.IsEdited,
        comment.CreatedAt,
        comment.UpdatedAt);
}
