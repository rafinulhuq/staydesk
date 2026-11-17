using StayDesk.Application.Rooms;

namespace StayDesk.UnitTests.Rooms;

// SRCH-2 (dates, 1–30 nights, guests 1–10) and SRCH-4 (paging limits).
public class RoomSearchRequestValidatorTests
{
    private static readonly RoomSearchRequestValidator Validator = new();
    private static readonly DateOnly Dec10 = new(2026, 12, 10);

    private static RoomSearchRequest Search(int nights = 3, int guests = 2, int page = 1, int pageSize = 20, decimal? maxPrice = null) => new()
    {
        City = "Berlin",
        CheckIn = Dec10,
        CheckOut = Dec10.AddDays(nights),
        Guests = guests,
        MaxPrice = maxPrice,
        Page = page,
        PageSize = pageSize,
    };

    private static string[] InvalidFields(RoomSearchRequest request) =>
        [.. Validator.Validate(request).Errors.Select(e => e.PropertyName).Distinct()];

    [Fact]
    public void A_complete_search_is_valid()
    {
        Assert.Empty(InvalidFields(Search()));
    }

    [Fact]
    public void City_is_required()
    {
        Assert.Equal(["City"], InvalidFields(new RoomSearchRequest { CheckIn = Dec10, CheckOut = Dec10.AddDays(1), Guests = 1 }));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(0, false)]
    [InlineData(31, false)]
    public void Stay_is_1_to_30_nights(int nights, bool valid)
    {
        Assert.Equal(valid, !InvalidFields(Search(nights: nights)).Contains("CheckOut"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void Guests_are_1_to_10(int guests, bool valid)
    {
        Assert.Equal(valid, !InvalidFields(Search(guests: guests)).Contains("Guests"));
    }

    [Theory]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void Page_starts_at_1_and_page_size_is_at_most_100(int page, int pageSize, bool valid)
    {
        Assert.Equal(valid, InvalidFields(Search(page: page, pageSize: pageSize)).Length == 0);
    }

    [Fact]
    public void Max_price_must_be_positive()
    {
        Assert.Equal(["MaxPrice"], InvalidFields(Search(maxPrice: 0m)));
    }
}
