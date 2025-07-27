using System.ComponentModel.DataAnnotations;

namespace SecurityMonitor.DTOs.Auth;

/// <summary>
/// DTO cho đăng nhập
/// </summary>
public record LoginDto(
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    string Email,

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [DataType(DataType.Password)]
    string Password,

    bool RememberMe
);

/// <summary>
/// DTO cho đăng ký tài khoản mới
/// </summary>
public record RegisterDto
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu không khớp")]
    [DataType(DataType.Password)]
    public required string ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Họ tên là bắt buộc")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Họ tên phải có ít nhất 2 ký tự")]
    public required string FullName { get; set; }
}

/// <summary>
/// DTO cho đổi mật khẩu
/// </summary>
public record ChangePasswordDto
{
    [Required(ErrorMessage = "Mật khẩu hiện tại là bắt buộc")]
    [DataType(DataType.Password)]
    public required string CurrentPassword { get; set; }

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    [DataType(DataType.Password)]
    public required string NewPassword { get; set; }

    [Required(ErrorMessage = "Xác nhận mật khẩu mới là bắt buộc")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu không khớp")]
    [DataType(DataType.Password)]
    public required string ConfirmNewPassword { get; set; }
}
