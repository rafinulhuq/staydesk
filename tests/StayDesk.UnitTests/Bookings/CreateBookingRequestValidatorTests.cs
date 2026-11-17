using StayDesk.Application.Bookings;

namespace StayDesk.UnitTests.Bookings;

// BOOK-1 guest details, SRCH-2 dates and BOOK-9 walk-in guest on the create-booking request.
public class CreateBookingRequestValidatorTests
{
    private static readonly CreateBookingRequestValidator Validator = new();

    private static CreateBookingRequest Request(
        string name = "Lena Schmidt", string email = "lena.schmidt@example.com", string? phone = "+49 30 1234567",
        string? specialRequests = null, int guests = 2, int nights = 2, decimal? expectedTotal = 258.00m) =>
        new(Guid.CreateVersion7(), new(2026, 12, 10), new DateOnly(2026, 12, 10).AddDays(nights), guests,
            name, email, phone, specialRequests, expectedTotal);

    private static string[] InvalidFields(CreateBookingRequest request) =>
        [.. Validator.Validate(request).Errors.Select(e => e.PropertyName).Distinct()];

    [Fact]
    public void A_complete_request_is_valid()
    {
        Assert.Empty(InvalidFields(Request()));
    }

    [Fact]
    public void Phone_special_requests_and_expected_total_are_optional()
    {
        Assert.Empty(InvalidFields(Request(phone: null, specialRequests: null, expectedTotal: null)));
    }

    [Fact]
    public void Guest_name_and_a_valid_email_are_required()
    {
        Assert.Equal(["GuestFullName", "GuestEmail"], InvalidFields(Request(name: " ", email: "lena.schmidt")));
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Special_requests_are_at_most_500_characters(int length, bool valid)
    {
        Assert.Equal(valid, InvalidFields(Request(specialRequests: new string('a', length))).Length == 0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Guests_are_1_to_10(int guests, bool valid)
    {
        Assert.Equal(valid, InvalidFields(Request(guests: guests)).Length == 0);
    }

    [Fact]
    public void A_walk_in_names_an_existing_guest_or_a_new_one_not_both()
    {
        var both = Request() with { GuestId = Guid.CreateVersion7(), NewGuest = new("Max", "Hoffmann", "max.hoffmann@example.com", null) };

        Assert.Equal(["GuestId"], InvalidFields(both));
    }

    [Fact]
    public void New_guest_details_need_names_and_a_valid_email()
    {
        var request = Request() with { NewGuest = new("", "Hoffmann", "max.hoffmann", null) };

        Assert.Equal(["NewGuest.FirstName", "NewGuest.Email"], InvalidFields(request));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(31, false)]
    public void Stay_dates_follow_the_search_rules(int nights, bool valid)
    {
        Assert.Equal(valid, !InvalidFields(Request(nights: nights)).Contains("CheckOut"));
    }
}
