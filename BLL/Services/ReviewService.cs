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
        Task<List<ReviewItemViewModel>> GetReviewsPagedAsync(string type, int listingId, int page, int pageSize);
        Task<Review?> GetReviewByBookingAsync(string bookingType, int bookingId);
        Task<(bool Success, string Message)> ReplyToReviewAsync(string ownerId, int reviewId, string reply);
        Task RecomputeAllAggregatesAsync();
    }

    public class ReviewService : IReviewService
    {
        private readonly IRepository<Review> _reviews;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly IRepository<Equipment> _equipment;
        private readonly IRepository<Godown> _godowns;
        private readonly IRepository<ApplicationUser> _users;
        private readonly INotificationService _notifications;

        public ReviewService(
            IRepository<Review> reviews,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            IRepository<Equipment> equipment,
            IRepository<Godown> godowns,
            IRepository<ApplicationUser> users,
            INotificationService notifications)
        {
            _reviews = reviews;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _equipment = equipment;
            _godowns = godowns;
            _users = users;
            _notifications = notifications;
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

            try
            {
                if (isEquipment)
                {
                    var booking = await _equipmentBookings.QueryTracked()
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

                    // Recalculate average rating & review count for the Equipment
                    var existingListingRatings = await _reviews.Query()
                        .Where(r => r.EquipmentId == booking.EquipmentId)
                        .Select(r => r.Rating)
                        .ToListAsync();

                    var newListingRatings = existingListingRatings.Append(model.Rating).ToList();
                    var count = newListingRatings.Count;
                    var avg = Math.Round(newListingRatings.Average(), 1);

                    var eq = await _equipment.QueryTracked().FirstOrDefaultAsync(e => e.Id == booking.EquipmentId);
                    if (eq != null)
                    {
                        eq.AverageRating = avg;
                        eq.ReviewCount = count;
                    }

                    // Recalculate owner aggregate across all their listings
                    var ownerId = eq?.OwnerId ?? booking.Equipment?.OwnerId;
                    if (!string.IsNullOrEmpty(ownerId))
                    {
                        var existingOwnerRatings = await _reviews.Query()
                            .Where(r => (r.Equipment != null && r.Equipment.OwnerId == ownerId) || (r.Godown != null && r.Godown.OwnerId == ownerId))
                            .Select(r => r.Rating)
                            .ToListAsync();

                        var newOwnerRatings = existingOwnerRatings.Append(model.Rating).ToList();
                        var ownerCount = newOwnerRatings.Count;
                        var ownerAvg = Math.Round(newOwnerRatings.Average(), 1);

                        var ownerUser = await _users.QueryTracked().FirstOrDefaultAsync(u => u.Id == ownerId);
                        if (ownerUser != null)
                        {
                            ownerUser.OwnerAverageRating = ownerAvg;
                            ownerUser.OwnerReviewCount = ownerCount;
                        }
                    }

                    // Single atomic SaveChangesAsync
                    await _reviews.SaveChangesAsync();

                    // Notify equipment owner of new review
                    if (eq != null && !string.IsNullOrEmpty(eq.OwnerId))
                    {
                        var farmer = await _users.FirstOrDefaultAsync(u => u.Id == farmerId);
                        var farmerName = farmer?.FullName ?? "A farmer";
                        await _notifications.NotifyAsync(new NotificationRequest
                        {
                            UserId = eq.OwnerId,
                            Type = NotificationTypes.ReviewReceived,
                            TitleKey = "New Review Received",
                            MessageKey = "{0} left a {1}★ review for {2}.",
                            Args = new object[] { farmerName, model.Rating, eq.Name },
                            LinkUrl = AppLinks.EquipmentDetails(eq.Id),
                            DedupeKey = $"review:{review.Id}",
                            SendEmail = false
                        });
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
                    var booking = await _godownBookings.QueryTracked()
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

                    // Recalculate average rating & review count for the Godown
                    var existingListingRatings = await _reviews.Query()
                        .Where(r => r.GodownId == booking.GodownId)
                        .Select(r => r.Rating)
                        .ToListAsync();

                    var newListingRatings = existingListingRatings.Append(model.Rating).ToList();
                    var count = newListingRatings.Count;
                    var avg = Math.Round(newListingRatings.Average(), 1);

                    var gd = await _godowns.QueryTracked().FirstOrDefaultAsync(g => g.Id == booking.GodownId);
                    if (gd != null)
                    {
                        gd.AverageRating = avg;
                        gd.ReviewCount = count;
                    }

                    // Recalculate owner aggregate across all their listings
                    var ownerId = gd?.OwnerId ?? booking.Godown?.OwnerId;
                    if (!string.IsNullOrEmpty(ownerId))
                    {
                        var existingOwnerRatings = await _reviews.Query()
                            .Where(r => (r.Equipment != null && r.Equipment.OwnerId == ownerId) || (r.Godown != null && r.Godown.OwnerId == ownerId))
                            .Select(r => r.Rating)
                            .ToListAsync();

                        var newOwnerRatings = existingOwnerRatings.Append(model.Rating).ToList();
                        var ownerCount = newOwnerRatings.Count;
                        var ownerAvg = Math.Round(newOwnerRatings.Average(), 1);

                        var ownerUser = await _users.QueryTracked().FirstOrDefaultAsync(u => u.Id == ownerId);
                        if (ownerUser != null)
                        {
                            ownerUser.OwnerAverageRating = ownerAvg;
                            ownerUser.OwnerReviewCount = ownerCount;
                        }
                    }

                    // Single atomic SaveChangesAsync
                    await _reviews.SaveChangesAsync();

                    // Notify godown owner of new review
                    if (gd != null && !string.IsNullOrEmpty(gd.OwnerId))
                    {
                        var farmer = await _users.FirstOrDefaultAsync(u => u.Id == farmerId);
                        var farmerName = farmer?.FullName ?? "A farmer";
                        await _notifications.NotifyAsync(new NotificationRequest
                        {
                            UserId = gd.OwnerId,
                            Type = NotificationTypes.ReviewReceived,
                            TitleKey = "New Review Received",
                            MessageKey = "{0} left a {1}★ review for {2}.",
                            Args = new object[] { farmerName, model.Rating, gd.Name },
                            LinkUrl = AppLinks.GodownDetails(gd.Id),
                            DedupeKey = $"review:{review.Id}",
                            SendEmail = false
                        });
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
            catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
            {
                return new ReviewSubmissionResult
                {
                    Success = false,
                    Message = "You have already submitted a review for this booking."
                };
            }
        }

        public async Task<ReviewsListViewModel> GetReviewsForEquipmentAsync(int equipmentId)
        {
            var ratingCounts = await _reviews.Query()
                .Where(r => r.EquipmentId == equipmentId)
                .GroupBy(r => r.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync();

            var total = ratingCounts.Sum(x => x.Count);
            var sum = ratingCounts.Sum(x => x.Rating * x.Count);
            var avg = total > 0 ? Math.Round((double)sum / total, 1) : 0.0;

            var breakdown = new RatingBreakdownViewModel
            {
                TotalReviews = total,
                FiveStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 5)?.Count ?? 0,
                FourStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 4)?.Count ?? 0,
                ThreeStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 3)?.Count ?? 0,
                TwoStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 2)?.Count ?? 0,
                OneStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 1)?.Count ?? 0
            };

            var firstPageReviews = await _reviews.Query()
                .Include(r => r.Farmer)
                .Where(r => r.EquipmentId == equipmentId)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(5)
                .ToListAsync();

            var items = firstPageReviews.Select(r => new ReviewItemViewModel
            {
                Id = r.Id,
                FarmerName = r.Farmer?.FullName ?? "Farmer",
                FarmerLocation = r.Farmer?.District ?? r.Farmer?.Location,
                Rating = r.Rating,
                Comment = r.Comment,
                OwnerReply = r.OwnerReply,
                OwnerRepliedAt = r.OwnerRepliedAt,
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

        public async Task<ReviewsListViewModel> GetReviewsForGodownAsync(int godownId)
        {
            var ratingCounts = await _reviews.Query()
                .Where(r => r.GodownId == godownId)
                .GroupBy(r => r.Rating)
                .Select(g => new { Rating = g.Key, Count = g.Count() })
                .ToListAsync();

            var total = ratingCounts.Sum(x => x.Count);
            var sum = ratingCounts.Sum(x => x.Rating * x.Count);
            var avg = total > 0 ? Math.Round((double)sum / total, 1) : 0.0;

            var breakdown = new RatingBreakdownViewModel
            {
                TotalReviews = total,
                FiveStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 5)?.Count ?? 0,
                FourStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 4)?.Count ?? 0,
                ThreeStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 3)?.Count ?? 0,
                TwoStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 2)?.Count ?? 0,
                OneStarCount = ratingCounts.FirstOrDefault(x => x.Rating == 1)?.Count ?? 0
            };

            var firstPageReviews = await _reviews.Query()
                .Include(r => r.Farmer)
                .Where(r => r.GodownId == godownId)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(5)
                .ToListAsync();

            var items = firstPageReviews.Select(r => new ReviewItemViewModel
            {
                Id = r.Id,
                FarmerName = r.Farmer?.FullName ?? "Farmer",
                FarmerLocation = r.Farmer?.District ?? r.Farmer?.Location,
                Rating = r.Rating,
                Comment = r.Comment,
                OwnerReply = r.OwnerReply,
                OwnerRepliedAt = r.OwnerRepliedAt,
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

        public async Task<List<ReviewItemViewModel>> GetReviewsPagedAsync(string type, int listingId, int page, int pageSize)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 5;

            var query = _reviews.Query().Include(r => r.Farmer).AsQueryable();
            if (type.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(r => r.EquipmentId == listingId);
            }
            else
            {
                query = query.Where(r => r.GodownId == listingId);
            }

            var reviews = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return reviews.Select(r => new ReviewItemViewModel
            {
                Id = r.Id,
                FarmerName = r.Farmer?.FullName ?? "Farmer",
                FarmerLocation = r.Farmer?.District ?? r.Farmer?.Location,
                Rating = r.Rating,
                Comment = r.Comment,
                OwnerReply = r.OwnerReply,
                OwnerRepliedAt = r.OwnerRepliedAt,
                CreatedAt = r.CreatedAt,
                TimeAgo = TimeAgoFormatter.Format(r.CreatedAt)
            }).ToList();
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

        public async Task<(bool Success, string Message)> ReplyToReviewAsync(string ownerId, int reviewId, string reply)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return (false, "User authentication required.");

            if (string.IsNullOrWhiteSpace(reply))
                return (false, "Reply cannot be empty.");

            var cleanReply = reply.Trim();
            if (cleanReply.Length > 1000)
                return (false, "Reply cannot exceed 1000 characters.");

            var review = await _reviews.QueryTracked()
                .Include(r => r.Equipment)
                .Include(r => r.Godown)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review == null)
                return (false, "Review not found.");

            var reviewOwnerId = review.Equipment?.OwnerId ?? review.Godown?.OwnerId;
            if (reviewOwnerId != ownerId)
                return (false, "You are not authorized to reply to this review.");

            review.OwnerReply = cleanReply;
            review.OwnerRepliedAt = DateTime.UtcNow;
            await _reviews.SaveChangesAsync();

            return (true, "Reply posted successfully.");
        }

        public async Task RecomputeAllAggregatesAsync()
        {
            var equipmentList = await _equipment.QueryTracked().ToListAsync();
            foreach (var eq in equipmentList)
            {
                var ratings = await _reviews.Query()
                    .Where(r => r.EquipmentId == eq.Id)
                    .Select(r => r.Rating)
                    .ToListAsync();

                eq.ReviewCount = ratings.Count;
                eq.AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0.0;
            }

            var godownList = await _godowns.QueryTracked().ToListAsync();
            foreach (var gd in godownList)
            {
                var ratings = await _reviews.Query()
                    .Where(r => r.GodownId == gd.Id)
                    .Select(r => r.Rating)
                    .ToListAsync();

                gd.ReviewCount = ratings.Count;
                gd.AverageRating = ratings.Count > 0 ? Math.Round(ratings.Average(), 1) : 0.0;
            }

            var owners = await _users.QueryTracked()
                .Where(u => u.UserRole == AppRoles.EquipmentOwner || u.UserRole == AppRoles.GodownOwner)
                .ToListAsync();

            foreach (var owner in owners)
            {
                var ownerRatings = await _reviews.Query()
                    .Where(r => (r.Equipment != null && r.Equipment.OwnerId == owner.Id) || (r.Godown != null && r.Godown.OwnerId == owner.Id))
                    .Select(r => r.Rating)
                    .ToListAsync();

                owner.OwnerReviewCount = ownerRatings.Count;
                owner.OwnerAverageRating = ownerRatings.Count > 0 ? Math.Round(ownerRatings.Average(), 1) : 0.0;
            }

            await _reviews.SaveChangesAsync();
        }
    }
}
