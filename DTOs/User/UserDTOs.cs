using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ProjectLedger.API.DTOs.User;

// ── Requests ────────────────────────────────────────────────

/// <summary>Request to update profile. Only fields the user can change.</summary>
public class UpdateProfileRequest
{
    [Required]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "Full name must be between 1 and 255 characters.")]
    public string FullName { get; set; } = null!;

    private string? _avatarUrl;

    [Url(ErrorMessage = "Avatar URL must be a valid URL.")]
    [RegularExpression(@"^https?://", ErrorMessage = "Avatar URL must be a valid http/https URL.")]
    public string? AvatarUrl
    {
        get => _avatarUrl;
        set
        {
            _avatarUrl = value;
            AvatarUrlSpecified = true;
        }
    }

    [JsonIgnore]
    public bool AvatarUrlSpecified { get; private set; }
}

/// <summary>Request to change password.</summary>
public class ChangePasswordRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Current password is required.")]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [StringLength(128, ErrorMessage = "New password cannot exceed 128 characters.")]
    public string NewPassword { get; set; } = null!;
}

// ── Responses ───────────────────────────────────────────────

/// <summary>Public profile response (non-sensitive data).</summary>
public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserPlanSummaryDto Plan { get; set; } = null!;
}

/// <summary>Summary of the plan assigned to the user.</summary>
public class UserPlanSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Slug { get; set; } = null!;
}

/// <summary>Minimal user response (for lists, members, etc.).</summary>
public class UserSummaryResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}
