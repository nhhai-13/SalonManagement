using SalonManagement.Controllers;
using Xunit;

namespace SalonManagement.Tests;

public class LoginAttemptMessageTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(1)]
    public void InvalidPasswordMessage_ShouldShowRemainingAttempts(int remainingAttempts)
    {
        var message = AuthController.GetInvalidCredentialsMessage(remainingAttempts);

        Assert.Equal(
            $"Email hoặc mật khẩu không chính xác. Bạn còn {remainingAttempts} lần nhập mật khẩu trước khi tài khoản bị khóa.",
            message);
    }
}
