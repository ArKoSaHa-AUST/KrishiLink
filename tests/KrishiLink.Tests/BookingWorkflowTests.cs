using KrishiLink.BLL.Services;
using KrishiLink.Models.Entities;

namespace KrishiLink.Tests;

public class BookingWorkflowTests
{
    private static readonly string[] Statuses =
    {
        BookingStatus.Pending, BookingStatus.Accepted, BookingStatus.Paid,
        BookingStatus.Rejected, BookingStatus.Completed, BookingStatus.Cancelled
    };

    private static readonly string[] Decisions = { "accept", "reject", "undo", "complete", BookingWorkflow.PaidDecision };

    // The only edges of the state machine, written out independently of the implementation.
    private static readonly Dictionary<(string Status, string Decision), string> Edges = new()
    {
        [(BookingStatus.Pending, "accept")] = BookingStatus.Accepted,
        [(BookingStatus.Pending, "reject")] = BookingStatus.Rejected,
        [(BookingStatus.Accepted, "undo")] = BookingStatus.Pending,
        [(BookingStatus.Rejected, "undo")] = BookingStatus.Pending,
        [(BookingStatus.Accepted, BookingWorkflow.PaidDecision)] = BookingStatus.Paid,
        [(BookingStatus.Paid, "complete")] = BookingStatus.Completed,
        [(BookingStatus.Completed, "undo")] = BookingStatus.Paid,
    };

    public static IEnumerable<object[]> AllCombinations() =>
        from status in Statuses
        from decision in Decisions
        from paid in new[] { false, true }
        from paidOut in new[] { false, true }
        select new object[] { status, decision, paid, paidOut };

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Next_matches_the_documented_edges(string status, string decision, bool paid, bool paidOut)
    {
        _ = paid;
        _ = paidOut;
        var expected = Edges.TryGetValue((status, decision), out var next) ? next : null;
        Assert.Equal(expected, BookingWorkflow.Next(status, decision));
        Assert.Equal(expected, BookingWorkflow.Next(status, decision.ToUpperInvariant()));
    }

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void Owner_decisions_are_allowed_only_on_legal_edges(string status, string decision, bool paid, bool paidOut)
    {
        var booking = Booking(status, paid, paidOut);
        var next = BookingWorkflow.Next(status, decision);

        var allowed = decision switch
        {
            // Payment is confirmed by the checkout, never by an owner decision.
            BookingWorkflow.PaidDecision => false,
            _ when !Edges.ContainsKey((status, decision)) => false,
            // A farmer's money is in escrow: the owner must cancel/refund instead of undoing.
            "undo" when status == BookingStatus.Accepted && paid => false,
            // Settled bookings are frozen.
            "undo" when status == BookingStatus.Completed && paidOut => false,
            _ => true
        };

        var error = BookingWorkflow.Guard(booking, decision, next);
        Assert.Equal(allowed, error is null);
    }

    [Fact]
    public void Complete_without_payment_is_refused_with_a_payment_message()
    {
        var booking = Booking(BookingStatus.Accepted, paid: false, paidOut: false);
        var error = BookingWorkflow.Guard(booking, "complete", BookingWorkflow.Next(booking.Status, "complete"));
        Assert.Equal("This booking can't be completed until the farmer has paid.", error);
    }

    [Fact]
    public void Undo_after_payout_is_refused()
    {
        var booking = Booking(BookingStatus.Completed, paid: true, paidOut: true);
        var error = BookingWorkflow.Guard(booking, "undo", BookingWorkflow.Next(booking.Status, "undo"));
        Assert.Equal("This booking has already been paid out and can no longer be changed.", error);
    }

    [Fact]
    public void Undo_of_an_accepted_booking_the_farmer_already_paid_is_refused()
    {
        var booking = Booking(BookingStatus.Accepted, paid: true, paidOut: false);
        var error = BookingWorkflow.Guard(booking, "undo", BookingWorkflow.Next(booking.Status, "undo"));
        Assert.Equal("This booking has been paid by the farmer; cancel and refund it instead of undoing.", error);
    }

    [Fact]
    public void Owner_cannot_mark_a_booking_paid()
    {
        var booking = Booking(BookingStatus.Accepted, paid: false, paidOut: false);
        var error = BookingWorkflow.Guard(booking, "paid", BookingWorkflow.Next(booking.Status, "paid"));
        Assert.Equal("Payment is confirmed by the farmer's checkout, not by the owner.", error);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-01-05", "2026-01-05", "2026-01-09", true)]
    [InlineData("2026-01-01", "2026-01-05", "2026-01-06", "2026-01-09", false)]
    [InlineData("2026-01-03", "2026-01-03", "2026-01-01", "2026-01-05", true)]
    [InlineData("2026-01-10", "2026-01-12", "2026-01-01", "2026-01-09", false)]
    public void Overlaps_is_inclusive_on_both_ends(string aStart, string aEnd, string bStart, string bEnd, bool expected)
    {
        var result = BookingWorkflow.Overlaps(DateTime.Parse(aStart), DateTime.Parse(aEnd), DateTime.Parse(bStart), DateTime.Parse(bEnd));
        Assert.Equal(expected, result);
        Assert.Equal(expected, BookingWorkflow.Overlaps(DateTime.Parse(bStart), DateTime.Parse(bEnd), DateTime.Parse(aStart), DateTime.Parse(aEnd)));
    }

    [Fact]
    public void Accept_snapshots_price_and_commission()
    {
        var booking = Booking(BookingStatus.Pending, paid: false, paidOut: false);
        var ledger = new RecordingLedger();

        BookingWorkflow.ApplyMoney(booking, BookingStatus.Accepted, "Equipment", "owner", ledger, 1500m, 4500m, 0.05m);

        Assert.Equal(1500m, booking.AgreedRate);
        Assert.Equal(4500m, booking.AgreedGross);
        Assert.Equal(0.05m, booking.CommissionRate);
        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public void Complete_posts_commission_and_undo_reverses_it()
    {
        var booking = Booking(BookingStatus.Paid, paid: true, paidOut: false);
        booking.AgreedGross = 4500m;
        booking.CommissionRate = 0.05m;
        var ledger = new RecordingLedger();

        BookingWorkflow.ApplyMoney(booking, BookingStatus.Completed, "Equipment", "owner", ledger, 0m, 0m, 0m);
        booking.Status = BookingStatus.Completed;
        BookingWorkflow.ApplyMoney(booking, BookingStatus.Paid, "Equipment", "owner", ledger, 0m, 0m, 0m);

        Assert.Collection(ledger.Entries,
            earned =>
            {
                Assert.Equal(LedgerEntryType.CommissionEarned, earned.Type);
                Assert.Equal(225m, earned.Amount);
                Assert.Equal(LedgerAccount.PlatformEscrow, earned.DebitAccount);
                Assert.Equal(LedgerAccount.PlatformCommission, earned.CreditAccount);
            },
            reversed =>
            {
                Assert.Equal(LedgerEntryType.CommissionReversed, reversed.Type);
                Assert.Equal(225m, reversed.Amount);
                Assert.Equal(LedgerAccount.PlatformCommission, reversed.DebitAccount);
                Assert.Equal(LedgerAccount.PlatformEscrow, reversed.CreditAccount);
            });
        Assert.Null(booking.CompletedOn);
    }

    [Fact]
    public void Zero_commission_posts_no_ledger_row()
    {
        var booking = Booking(BookingStatus.Paid, paid: true, paidOut: false);
        booking.AgreedGross = 4500m;
        booking.CommissionRate = 0m;
        var ledger = new RecordingLedger();

        BookingWorkflow.ApplyMoney(booking, BookingStatus.Completed, "Equipment", "owner", ledger, 0m, 0m, 0m);

        Assert.Empty(ledger.Entries);
        Assert.NotNull(booking.CompletedOn);
    }

    private static EquipmentBooking Booking(string status, bool paid, bool paidOut) => new()
    {
        Id = 1,
        FarmerId = "farmer",
        Status = status,
        StartDate = DateTime.Today.AddDays(5),
        EndDate = DateTime.Today.AddDays(7),
        Payment = paid ? new Payment { Id = 9, Status = PaymentStatus.Succeeded, Amount = 4500m } : null,
        PaymentId = paid ? 9 : null,
        PayoutId = paidOut ? 3 : null
    };
}
