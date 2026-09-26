using System.ComponentModel.DataAnnotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SalonManagement.Models.ViewModels;

namespace SalonManagement.Tests;

/// <summary>
/// Unit tests cho ViewModels — kiểm tra validation rules cho AC1 và AC2.
/// </summary>
[TestClass]
public class PasswordViewModelValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var ctx = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, ctx, results, validateAllProperties: true);
        return results;
    }

    // ═══════════════════════════════════════════════════════════════
    // AC1: ChangePasswordViewModel
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("AC1: Mật khẩu mới đủ điều kiện (8+ ký tự, có chữ và số)")]
    public void ChangePasswordViewModel_ValidPassword_NoErrors()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "OldPass1",
            NewPassword = "NewPass1",
            ConfirmPassword = "NewPass1"
        };
        var results = Validate(model);
        Assert.AreEqual(0, results.Count, "Model hợp lệ không được có lỗi validation");
    }

    [TestMethod]
    [Description("AC1: Mật khẩu mới < 8 ký tự phải bị từ chối")]
    public void ChangePasswordViewModel_ShortPassword_HasError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old",
            NewPassword = "abc1",      // chỉ 4 ký tự
            ConfirmPassword = "abc1"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("NewPassword")),
            "NewPassword < 8 ký tự phải có lỗi");
    }

    [TestMethod]
    [Description("AC1: Mật khẩu mới không có số phải bị từ chối")]
    public void ChangePasswordViewModel_PasswordWithoutDigit_HasError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old",
            NewPassword = "OnlyLetters",   // không có số
            ConfirmPassword = "OnlyLetters"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("NewPassword")),
            "Mật khẩu không có số phải có lỗi");
    }

    [TestMethod]
    [Description("AC1: Mật khẩu mới không có chữ phải bị từ chối")]
    public void ChangePasswordViewModel_PasswordWithoutLetter_HasError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old",
            NewPassword = "12345678",   // không có chữ
            ConfirmPassword = "12345678"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("NewPassword")),
            "Mật khẩu không có chữ phải có lỗi");
    }

    [TestMethod]
    [Description("AC1: Xác nhận mật khẩu không khớp phải bị từ chối")]
    public void ChangePasswordViewModel_ConfirmMismatch_HasError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "old",
            NewPassword = "NewPass1",
            ConfirmPassword = "DifferentPass1"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("ConfirmPassword")),
            "Xác nhận không khớp phải có lỗi");
    }

    [TestMethod]
    [Description("AC1: Mật khẩu hiện tại bắt buộc phải nhập")]
    public void ChangePasswordViewModel_EmptyCurrentPassword_HasError()
    {
        var model = new ChangePasswordViewModel
        {
            CurrentPassword = "",
            NewPassword = "NewPass1",
            ConfirmPassword = "NewPass1"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("CurrentPassword")),
            "CurrentPassword rỗng phải có lỗi");
    }

    // ═══════════════════════════════════════════════════════════════
    // AC2: ForgotPasswordViewModel
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("AC2: Email hợp lệ không có lỗi validation")]
    public void ForgotPasswordViewModel_ValidEmail_NoErrors()
    {
        var model = new ForgotPasswordViewModel { Email = "user@salon.vn" };
        var results = Validate(model);
        Assert.AreEqual(0, results.Count, "Email hợp lệ không được có lỗi");
    }

    [TestMethod]
    [Description("AC2: Email không đúng định dạng phải bị từ chối")]
    public void ForgotPasswordViewModel_InvalidEmail_HasError()
    {
        var model = new ForgotPasswordViewModel { Email = "not-an-email" };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("Email")),
            "Email không hợp lệ phải có lỗi");
    }

    [TestMethod]
    [Description("AC2: Email rỗng phải bị từ chối")]
    public void ForgotPasswordViewModel_EmptyEmail_HasError()
    {
        var model = new ForgotPasswordViewModel { Email = "" };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("Email")),
            "Email rỗng phải có lỗi");
    }

    // ═══════════════════════════════════════════════════════════════
    // AC2: ResetPasswordViewModel
    // ═══════════════════════════════════════════════════════════════

    [TestMethod]
    [Description("AC2: ResetPassword hợp lệ (email + token + mật khẩu đủ điều kiện)")]
    public void ResetPasswordViewModel_AllValid_NoErrors()
    {
        var model = new ResetPasswordViewModel
        {
            Email = "user@salon.vn",
            Token = "some-valid-token",
            NewPassword = "Reset1234",
            ConfirmPassword = "Reset1234"
        };
        var results = Validate(model);
        Assert.AreEqual(0, results.Count, "Model hợp lệ không được có lỗi");
    }

    [TestMethod]
    [Description("AC2: ResetPassword với mật khẩu mới < 8 ký tự phải bị từ chối")]
    public void ResetPasswordViewModel_ShortNewPassword_HasError()
    {
        var model = new ResetPasswordViewModel
        {
            Email = "user@salon.vn",
            Token = "token",
            NewPassword = "Ab1",
            ConfirmPassword = "Ab1"
        };
        var results = Validate(model);
        Assert.IsTrue(results.Any(r => r.MemberNames.Contains("NewPassword")),
            "NewPassword < 8 ký tự phải có lỗi");
    }
}
