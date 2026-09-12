using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public class ReviewSubmissionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public double NewAverageRating { get; set; }
        public int NewTotalReviews { get; set; }
        public int ReviewId { get; set; }
    }

    public interface IReviewService
    {
        Task<ReviewSubmissionResult> SubmitReviewAsync(string farmerId, SubmitReviewViewModel model);
        Task<ReviewsListViewModel> GetReviewsForEquipmentAsync(int equipmentId);
        Task<ReviewsListViewModel> GetReviewsForGodownAsync(int godownId);
        Task<Review?> GetReviewByBookingAsync(string bookingType, int bookingId);
    }

    public class ReviewService : IReviewService
    {
        private readonly IRepository<Review> _reviews;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<Godown> _godowns;

        public ReviewService(
            IRepository<Review> reviews,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IRepository<Equipment> equipment,
            IRepository<Godown> godowns)
        {
            _reviews = reviews;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _equipment = equipment;
            _godowns = godowns;
        }

        public async Task<ReviewSubmissionResult> SubmitReviewAsync(string farmerId, SubmitReviewViewModel model)
        {
            if (string.IsNullOrWhiteSpace(farmerId))
            {
                return new ReviewSubmissionResult { Success = false, Message = "User authentication required." };
            }

            if (model.Rating < 1 || model.Rating > 5)
            {
                return new ReviewSubmissionResult { Success = false, Message = "Rating must be between 1 and 5 stars." };
            }

            var isEquipment = model.BookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase);

            if (isEquipment)
            {
                var booking = await _equipmentBookings.Query()
                    .Include(b => b.Equipment)
                    .Include(b => b.Review)
                    .FirstOrDefaultAsync(b => b.Id == model.BookingId);

                if (booking is null)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "Booking not found." };
                }

                if (booking.FarmerId != farmerId)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "You are not authorized to review this booking." };
                }

                if (booking.Status != BookingStatus.Completed)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "Reviews can only be submitted for completed bookings." };
                }

                if (booking.Review != null || await _reviews.Query().AnyAsync(r => r.EquipmentBookingId == booking.Id))
                {
                    return new ReviewSubmissionResult { Success = false, Message = "You have already submitted a review for this booking." };
                }

                var review = new Review
                {
                    FarmerId = farmerId,
                    Rating = model.Rating,
                    Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    BookingType = "Equipment",
                    EquipmentId = booking.EquipmentId,
                    EquipmentBookingId = booking.Id
                };

                await _reviews.AddAsync(review);
                await _reviews.SaveChangesAsync();

                // Recalculate average rating & review count for the Equipment
                var allEquipmentReviews = await _reviews.Query()
                    .Where(r => r.EquipmentId == booking.EquipmentId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                var count = allEquipmentReviews.Count;
                var avg = count > 0 ? Math.Round(allEquipmentReviews.Average(), 1) : 0.0;

                var eq = booking.Equipment ?? await _equipment.GetByIdAsync(booking.EquipmentId);
                if (eq != null)
                {
                    eq.AverageRating = avg;
                    eq.ReviewCount = count;
                    _equipment.Update(eq);
                    await _equipment.SaveChangesAsync();
                }

                return new ReviewSubmissionResult
                {
                    Success = true,
                    Message = "Thank you! Your review has been submitted.",
                    NewAverageRating = avg,
                    NewTotalReviews = count,
                    ReviewId = review.Id
                };
            }
            else
            {
                var booking = await _godownBookings.Query()
                    .Include(b => b.Godown)
                    .Include(b => b.Review)
                    .FirstOrDefaultAsync(b => b.Id == model.BookingId);

                if (booking is null)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "Booking not found." };
                }

                if (booking.FarmerId != farmerId)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "You are not authorized to review this booking." };
                }

                if (booking.Status != BookingStatus.Completed)
                {
                    return new ReviewSubmissionResult { Success = false, Message = "Reviews can only be submitted for completed bookings." };
                }

                if (booking.Review != null || await _reviews.Query().AnyAsync(r => r.GodownBookingId == booking.Id))
                {
                    return new ReviewSubmissionResult { Success = false, Message = "You have already submitted a review for this booking." };
                }

                var review = new Review
                {
                    FarmerId = farmerId,
                    Rating = model.Rating,
                    Comment = string.IsNullOrWhiteSpace(model.Comment) ? null : model.Comment.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    BookingType = "Godown",
                    GodownId = booking.GodownId,
                    GodownBookingId = booking.Id
                };

                await _reviews.AddAsync(review);
                await _reviews.SaveChangesAsync();

                // Recalculate average rating & review count for the Godown
                var allGodownReviews = await _reviews.Query()
                    .Where(r => r.GodownId == booking.GodownId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                var count = allGodownReviews.Count;
                var avg = count > 0 ? Math.Round(allGodownReviews.Average(), 1) : 0.0;

                var gd = booking.Godown ?? await _godowns.GetByIdAsync(booking.GodownId);
                if (gd != null)
                {
                    gd.AverageRating = avg;
                    gd.ReviewCount = count;
                    _godowns.Update(gd);
                    await _godowns.SaveChangesAsync();
                }

                return new ReviewSubmissionResult
                {
                    Success = true,
                    Message = "Thank you! Your review has been submitted.",
                    NewAverageRating = avg,
                    NewTotalReviews = count,
                    ReviewId = review.Id
                };
            }
        }

        public async Task<ReviewsListViewModel> GetReviewsForEquipmentAsync(int equipmentId)
        {
            var reviews = await _reviews.Query()
                .Include(r => r.Farmer)
                .Where(r => r.EquipmentId == equipmentId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return BuildReviewList(reviews);
        }

        public async Task<ReviewsListViewModel> GetReviewsForGodownAsync(int godownId)
        {
            var reviews = await _reviews.Query()
                .Include(r => r.Farmer)
                .Where(r => r.GodownId == godownId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return BuildReviewList(reviews);
        }

        public async Task<Review?> GetReviewByBookingAsync(string bookingType, int bookingId)
        {
            if (bookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                return await _reviews.Query()
                    .Include(r => r.Farmer)
                    .FirstOrDefaultAsync(r => r.EquipmentBookingId == bookingId);
            }
            else
            {
                return await _reviews.Query()
                    .Include(r => r.Farmer)
                    .FirstOrDefaultAsync(r => r.GodownBookingId == bookingId);
            }
        }

        private static ReviewsListViewModel BuildReviewList(List<Review> reviews)
        {
            var total = reviews.Count;
            var avg = total > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 0.0;

            var breakdown = new RatingBreakdownViewModel
            {
                TotalReviews = total,
                FiveStarCount = reviews.Count(r => r.Rating == 5),
                FourStarCount = reviews.Count(r => r.Rating == 4),
                ThreeStarCount = reviews.Count(r => r.Rating == 3),
                TwoStarCount = reviews.Count(r => r.Rating == 2),
                OneStarCount = reviews.Count(r => r.Rating == 1)
            };

            var items = reviews.Select(r => new ReviewItemViewModel
            {
                Id = r.Id,
                FarmerName = r.Farmer?.FullName ?? "Farmer",
                FarmerLocation = r.Farmer?.District ?? r.Farmer?.Location,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt,
                TimeAgo = TimeAgoFormatter.Format(r.CreatedAt)
            }).ToList();

            return new ReviewsListViewModel
            {
                AverageRating = avg,
                TotalReviews = total,
                Breakdown = breakdown,
                Reviews = items
            };
        }
    }
}
