using StayDesk.Application.Auth;

namespace StayDesk.UnitTests.Auth;

// AUTH-1 input rules owned by the validator (password strength is Identity's).
public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator validator = new();

    [Fact]
    public void A_complete_request_is_valid()
    {
        var result = validator.Validate(new RegisterRequest("Anna", "Keller", "anna.keller@example.de", "Sommer2026"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("anna.keller")]
    [InlineData("@example.de")]
    public void An_invalid_email_is_rejected(string email)
    {
        var result = validator.Validate(new RegisterRequest("Anna", "Keller", email, "Sommer2026"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Email));
    }

    [Fact]
    public void Names_longer_than_the_column_are_rejected()
    {
        var result = validator.Validate(new RegisterRequest(new string('a', 101), "Keller", "anna.keller@example.de", "Sommer2026"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.FirstName));
    }
}
