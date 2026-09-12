using KrishiLink.DAL.Repositories;
using KrishiLink.Models.Entities;
using KrishiLink.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace KrishiLink.BLL.Services
{
    public interface ILoyaltyService
    {
        Task<LoyaltyHubViewModel> GetFarmerLoyaltyHubAsync(string farmerId);
        Task<FarmerDashboardLoyaltyWidgetViewModel> GetDashboardWidgetAsync(string farmerId);
        List<FixedConversionTierViewModel> GetConversionTiers();
        LoyaltyTierInfo CalculateTier(int lifetimePoints);
        Task<LoyaltyDiscountResult> ValidateAndCalculateDiscountAsync(string? farmerId, string? promoCode, int? pointsToRedeem, decimal totalBookingAmount);
        Task<(bool Success, string? Error, LoyaltyDiscountResult? Result)> RedeemPointsForBookingAsync(string farmerId, string? promoCode, int? pointsToRedeem, decimal totalBookingAmount, string bookingType, int bookingId, string? bookingCode);
        Task AwardPointsForCompletedBookingAsync(string farmerId, string bookingType, int bookingId, decimal amountSpent, string? bookingCode = null);
        Task RefundPointsForCancelledBookingAsync(string farmerId, string bookingType, int bookingId, string? bookingCode = null);
        Task<LoyaltyVoucherViewModel> GenerateVoucherAsync(string farmerId, int pointsTier);
        int CalculatePointsEarned(decimal amountSpent);
    }

    public class LoyaltyService : ILoyaltyService
    {
        private readonly IRepository<LoyaltyPointTransaction> _transactions;
        private readonly IRepository<EquipmentBooking> _equipmentBookings;
        private readonly IRepository<GodownBooking> _godownBookings;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notifications;

        public LoyaltyService(
            IRepository<LoyaltyPointTransaction> transactions,
            IRepository<EquipmentBooking> equipmentBookings,
            IRepository<GodownBooking> godownBookings,
            UserManager<ApplicationUser> userManager,
            INotificationService notifications)
        {
            _transactions = transactions;
            _equipmentBookings = equipmentBookings;
            _godownBookings = godownBookings;
            _userManager = userManager;
            _notifications = notifications;
        }

        public List<FixedConversionTierViewModel> GetConversionTiers()
        {
            return new List<FixedConversionTierViewModel>
            {
                new()
                {
                    PointsRequired = 50,
                    DiscountAmount = 50m,
                    Multiplier = 1.0m,
                    BonusText = "Standard Value",
                    PromoCodePrefix = "KRISHI-POINTS-50",
                    Description = "Redeem 50 KrishiPoints for an instant ৳50 discount.",
                    IsBestValue = false
                },
                new()
                {
                    PointsRequired = 100,
                    DiscountAmount = 120m,
                    Multiplier = 1.2m,
                    BonusText = "+20% Extra Value",
                    PromoCodePrefix = "KRISHI-POINTS-100",
                    Description = "Redeem 100 KrishiPoints for a ৳120 discount (+20% bonus value).",
                    IsBestValue = false
                },
                new()
                {
                    PointsRequired = 250,
                    DiscountAmount = 325m,
                    Multiplier = 1.3m,
                    BonusText = "+30% Extra Value",
                    PromoCodePrefix = "KRISHI-POINTS-250",
                    Description = "Redeem 250 KrishiPoints for a ৳325 discount (+30% bonus value).",
                    IsBestValue = true
                },
                new()
                {
                    PointsRequired = 500,
                    DiscountAmount = 700m,
                    Multiplier = 1.4m,
                    BonusText = "+40% Extra Value",
                    PromoCodePrefix = "KRISHI-POINTS-500",
                    Description = "Redeem 500 KrishiPoints for a ৳700 discount (+40% bonus value).",
                    IsBestValue = false
                },
                new()
                {
                    PointsRequired = 1000,
                    DiscountAmount = 1500m,
                    Multiplier = 1.5m,
                    BonusText = "+50% Super Saver",
                    PromoCodePrefix = "KRISHI-POINTS-1000",
                    Description = "Redeem 1,000 KrishiPoints for a massive ৳1,500 discount (+50% bonus value).",
                    IsBestValue = false
                }
            };
        }

        public LoyaltyTierInfo CalculateTier(int lifetimePoints)
        {
            if (lifetimePoints >= 1000)
            {
                return new LoyaltyTierInfo
                {
                    TierName = "Platinum Agronomist",
                    TierBadgeClass = "bg-primary text-white",
                    TierColor = "#2563eb",
                    TierIcon = "bi-gem",
                    MinPoints = 1000,
                    MaxPoints = int.MaxValue,
                    NextTierName = "Master Agronomist (Max Tier)",
                    PointsToNextTier = 0,
                    ProgressPercentage = 100,
                    Perks = new()
                    {
                        "50% maximum bonus on 1000pt redemption tier",
                        "Priority seasonal booking dispatch & emergency machinery queue",
                        "Exclusive verified farmer trust watermark on all rental passes"
                    }
                };
            }
            if (lifetimePoints >= 500)
            {
                int range = 1000 - 500;
                int progress = lifetimePoints - 500;
                int percent = (int)Math.Min(100, Math.Max(0, (progress * 100.0) / range));

                return new LoyaltyTierInfo
                {
                    TierName = "Gold Planter",
                    TierBadgeClass = "bg-warning text-dark",
                    TierColor = "#f59e0b",
                    TierIcon = "bi-trophy-fill",
                    MinPoints = 500,
                    MaxPoints = 1000,
                    NextTierName = "Platinum Agronomist",
                    PointsToNextTier = 1000 - lifetimePoints,
                    ProgressPercentage = percent,
                    Perks = new()
                    {
                        "40% bonus discount on 500pt redemption tiers",
                        "Zero cancellation fees up to 12 hours before rental",
                        "Early access to newly listed harvest combine harvesters"
                    }
                };
            }
            if (lifetimePoints >= 200)
            {
                int range = 500 - 200;
                int progress = lifetimePoints - 200;
                int percent = (int)Math.Min(100, Math.Max(0, (progress * 100.0) / range));

                return new LoyaltyTierInfo
                {
                    TierName = "Silver Cultivator",
                    TierBadgeClass = "bg-secondary text-white",
                    TierColor = "#64748b",
                    TierIcon = "bi-patch-check-fill",
                    MinPoints = 200,
                    MaxPoints = 500,
                    NextTierName = "Gold Planter",
                    PointsToNextTier = 500 - lifetimePoints,
                    ProgressPercentage = percent,
                    Perks = new()
                    {
                        "30% bonus discount on 250pt redemption tier",
                        "Priority rental and storage booking notifications",
                        "Direct SMS confirmation pass dispatch"
                    }
                };
            }

            int bronzeRange = 200;
            int bronzePercent = (int)Math.Min(100, Math.Max(0, (lifetimePoints * 100.0) / bronzeRange));
            return new LoyaltyTierInfo
            {
                TierName = "Bronze Harvester",
                TierBadgeClass = "bg-dark bg-opacity-75 text-white",
                TierColor = "#92400e",
                TierIcon = "bi-seedling",
                MinPoints = 0,
                MaxPoints = 200,
                NextTierName = "Silver Cultivator",
                PointsToNextTier = Math.Max(0, 200 - lifetimePoints),
                ProgressPercentage = bronzePercent,
                Perks = new()
                {
                    "Earn 1 KrishiPoint for every ৳100 spent on completed rentals",
                    "Redeem points starting from 50 points (৳50 OFF)",
                    "Digital booking verification & QR pass generator"
                }
            };
        }

        public int CalculatePointsEarned(decimal amountSpent)
        {
            if (amountSpent <= 0) return 10;
            int earned = (int)Math.Floor(amountSpent / 100m);
            return Math.Max(10, earned);
        }

        public async Task<LoyaltyHubViewModel> GetFarmerLoyaltyHubAsync(string farmerId)
        {
            var user = await _userManager.FindByIdAsync(farmerId);
            var currentPoints = user?.LoyaltyPoints ?? 0;

            var history = await _transactions.Query()
                .Where(t => t.UserId == farmerId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            int lifetimeEarned = history.Where(t => t.Points > 0).Sum(t => t.Points);
            int lifetimeRedeemed = Math.Abs(history.Where(t => t.Points < 0).Sum(t => t.Points));

            var completedEq = await _equipmentBookings.Query()
                .CountAsync(b => b.FarmerId == farmerId && b.Status == BookingStatus.Completed);
            var completedGd = await _godownBookings.Query()
                .CountAsync(b => b.FarmerId == farmerId && b.Status == BookingStatus.Completed);

            var activeVouchers = history
                .Where(t => t.Type == LoyaltyTransactionTypes.VoucherGenerated && !string.IsNullOrEmpty(t.PromoCode))
                .Select(t => new LoyaltyVoucherViewModel
                {
                    Code = t.PromoCode!,
                    DiscountAmount = t.DiscountAmount ?? 0m,
                    PointsCost = Math.Abs(t.Points),
                    GeneratedAt = t.CreatedAt,
                    IsRedeemed = history.Any(r => r.Type == LoyaltyTransactionTypes.Redeemed && r.PromoCode == t.PromoCode)
                })
                .Where(v => !v.IsRedeemed)
                .ToList();

            var tier = CalculateTier(lifetimeEarned > 0 ? lifetimeEarned : currentPoints);

            var transactionItems = history.Select(t => new PointTransactionItemViewModel
            {
                Id = t.Id,
                Points = t.Points,
                Type = t.Type,
                Description = t.Description,
                BookingType = t.BookingType,
                BookingId = t.BookingId,
                BookingCode = t.BookingCode,
                PromoCode = t.PromoCode,
                DiscountAmount = t.DiscountAmount,
                AmountSpent = t.AmountSpent,
                CreatedAt = t.CreatedAt,
                RelativeTime = FormatRelativeTime(t.CreatedAt)
            }).ToList();

            return new LoyaltyHubViewModel
            {
                FarmerId = farmerId,
                FarmerName = user?.FullName ?? "Farmer",
                CurrentPoints = currentPoints,
                EstimatedDiscountValue = currentPoints, // Minimum 1:1 baseline
                LifetimeEarnedPoints = lifetimeEarned,
                LifetimeRedeemedPoints = lifetimeRedeemed,
                CompletedBookingsCount = completedEq + completedGd,
                Tier = tier,
                ConversionTiers = GetConversionTiers(),
                ActiveVouchers = activeVouchers,
                Transactions = transactionItems
            };
        }

        public async Task<FarmerDashboardLoyaltyWidgetViewModel> GetDashboardWidgetAsync(string farmerId)
        {
            var user = await _userManager.FindByIdAsync(farmerId);
            var currentPoints = user?.LoyaltyPoints ?? 0;

            var lifetimeEarned = await _transactions.Query()
                .Where(t => t.UserId == farmerId && t.Points > 0)
                .SumAsync(t => t.Points);

            var tier = CalculateTier(lifetimeEarned > 0 ? lifetimeEarned : currentPoints);

            var vouchersCount = await _transactions.Query()
                .Where(t => t.UserId == farmerId && t.Type == LoyaltyTransactionTypes.VoucherGenerated && !string.IsNullOrEmpty(t.PromoCode))
                .Select(t => t.PromoCode)
                .Distinct()
                .CountAsync();

            return new FarmerDashboardLoyaltyWidgetViewModel
            {
                CurrentPoints = currentPoints,
                EstimatedDiscountValue = currentPoints,
                TierName = tier.TierName,
                TierBadgeClass = tier.TierBadgeClass,
                TierIcon = tier.TierIcon,
                TierIconClass = tier.TierIcon,
                DiscountMultiplier = tier.DiscountMultiplier,
                ActiveVouchersCount = vouchersCount,
                NextTierName = tier.NextTierName,
                PointsToNextTier = tier.PointsToNextTier,
                ProgressPercentage = tier.ProgressPercentage
            };
        }

        public async Task<LoyaltyDiscountResult> ValidateAndCalculateDiscountAsync(
            string? farmerId,
            string? promoCode,
            int? pointsToRedeem,
            decimal totalBookingAmount)
        {
            if (totalBookingAmount <= 0)
            {
                return new LoyaltyDiscountResult
                {
                    IsValid = false,
                    Message = "Booking amount must be greater than zero.",
                    OriginalTotal = totalBookingAmount,
                    DiscountedTotal = totalBookingAmount,
                    PointsToEarn = 0
                };
            }

            int pointsToEarn = CalculatePointsEarned(totalBookingAmount);

            // Case 1: Promo Code or Fixed Tier Code entered
            if (!string.IsNullOrWhiteSpace(promoCode))
            {
                var cleanCode = promoCode.Trim().ToUpperInvariant();
                var tiers = GetConversionTiers();
                var matchedTier = tiers.FirstOrDefault(t =>
                    t.PromoCodePrefix.Equals(cleanCode, StringComparison.OrdinalIgnoreCase) ||
                    $"KRISHI{t.PointsRequired}".Equals(cleanCode, StringComparison.OrdinalIgnoreCase));

                if (matchedTier != null)
                {
                    if (string.IsNullOrEmpty(farmerId))
                    {
                        return new LoyaltyDiscountResult
                        {
                            IsValid = false,
                            Message = "Please log in to redeem your loyalty points promo code.",
                            OriginalTotal = totalBookingAmount,
                            DiscountedTotal = totalBookingAmount,
                            PointsToEarn = pointsToEarn
                        };
                    }

                    var user = await _userManager.FindByIdAsync(farmerId);
                    int currentPoints = user?.LoyaltyPoints ?? 0;

                    if (currentPoints < matchedTier.PointsRequired)
                    {
                        return new LoyaltyDiscountResult
                        {
                            IsValid = false,
                            Message = $"Insufficient KrishiPoints. You need {matchedTier.PointsRequired} points for this code, but have {currentPoints} points.",
                            OriginalTotal = totalBookingAmount,
                            DiscountedTotal = totalBookingAmount,
                            PointsToEarn = pointsToEarn
                        };
                    }

                    decimal discount = Math.Min(matchedTier.DiscountAmount, totalBookingAmount);
                    decimal finalTotal = Math.Max(0m, totalBookingAmount - discount);

                    return new LoyaltyDiscountResult
                    {
                        IsValid = true,
                        DiscountAmount = discount,
                        PointsRequired = matchedTier.PointsRequired,
                        PromoCode = cleanCode,
                        Message = $"Promo code applied! ৳{discount:N0} discount ({matchedTier.BonusText}).",
                        OriginalTotal = totalBookingAmount,
                        DiscountedTotal = finalTotal,
                        PointsToEarn = CalculatePointsEarned(finalTotal),
                        RemainingPoints = currentPoints - matchedTier.PointsRequired
                    };
                }

                // Check active generated voucher
                if (!string.IsNullOrEmpty(farmerId))
                {
                    var activeVoucher = await _transactions.Query()
                        .FirstOrDefaultAsync(t => t.UserId == farmerId &&
                                                  t.Type == LoyaltyTransactionTypes.VoucherGenerated &&
                                                  t.PromoCode == cleanCode);

                    if (activeVoucher != null)
                    {
                        bool isAlreadyUsed = await _transactions.Query()
                            .AnyAsync(t => t.Type == LoyaltyTransactionTypes.Redeemed && t.PromoCode == cleanCode);

                        if (isAlreadyUsed)
                        {
                            return new LoyaltyDiscountResult
                            {
                                IsValid = false,
                                Message = "This voucher code has already been redeemed on another booking.",
                                OriginalTotal = totalBookingAmount,
                                DiscountedTotal = totalBookingAmount,
                                PointsToEarn = pointsToEarn
                            };
                        }

                        decimal discount = Math.Min(activeVoucher.DiscountAmount ?? 0m, totalBookingAmount);
                        decimal finalTotal = Math.Max(0m, totalBookingAmount - discount);

                        return new LoyaltyDiscountResult
                        {
                            IsValid = true,
                            DiscountAmount = discount,
                            PointsRequired = 0, // already deducted at generation
                            PromoCode = cleanCode,
                            Message = $"Loyalty voucher applied! ৳{discount:N0} discount.",
                            OriginalTotal = totalBookingAmount,
                            DiscountedTotal = finalTotal,
                            PointsToEarn = CalculatePointsEarned(finalTotal),
                            RemainingPoints = (await _userManager.FindByIdAsync(farmerId))?.LoyaltyPoints ?? 0
                        };
                    }
                }

                return new LoyaltyDiscountResult
                {
                    IsValid = false,
                    Message = "Invalid or expired promo code. Check your Loyalty Hub for available tiers.",
                    OriginalTotal = totalBookingAmount,
                    DiscountedTotal = totalBookingAmount,
                    PointsToEarn = pointsToEarn
                };
            }

            // Case 2: Direct Points Selection
            if (pointsToRedeem.HasValue && pointsToRedeem.Value > 0)
            {
                if (string.IsNullOrEmpty(farmerId))
                {
                    return new LoyaltyDiscountResult
                    {
                        IsValid = false,
                        Message = "Please log in to apply loyalty points.",
                        OriginalTotal = totalBookingAmount,
                        DiscountedTotal = totalBookingAmount,
                        PointsToEarn = pointsToEarn
                    };
                }

                var user = await _userManager.FindByIdAsync(farmerId);
                int currentPoints = user?.LoyaltyPoints ?? 0;

                if (pointsToRedeem.Value > currentPoints)
                {
                    return new LoyaltyDiscountResult
                    {
                        IsValid = false,
                        Message = $"You only have {currentPoints} points available.",
                        OriginalTotal = totalBookingAmount,
                        DiscountedTotal = totalBookingAmount,
                        PointsToEarn = pointsToEarn
                    };
                }

                if (pointsToRedeem.Value < 50)
                {
                    return new LoyaltyDiscountResult
                    {
                        IsValid = false,
                        Message = "Minimum point redemption is 50 KrishiPoints.",
                        OriginalTotal = totalBookingAmount,
                        DiscountedTotal = totalBookingAmount,
                        PointsToEarn = pointsToEarn
                    };
                }

                // Check if exact tier exists
                var tiers = GetConversionTiers();
                var tier = tiers.FirstOrDefault(t => t.PointsRequired == pointsToRedeem.Value);
                decimal discountAmount = tier != null ? tier.DiscountAmount : (decimal)pointsToRedeem.Value;

                decimal discount = Math.Min(discountAmount, totalBookingAmount);
                decimal finalTotal = Math.Max(0m, totalBookingAmount - discount);

                return new LoyaltyDiscountResult
                {
                    IsValid = true,
                    DiscountAmount = discount,
                    PointsRequired = pointsToRedeem.Value,
                    PromoCode = tier?.PromoCodePrefix ?? $"POINTS-{pointsToRedeem.Value}",
                    Message = $"Redeemed {pointsToRedeem.Value} KrishiPoints for ৳{discount:N0} discount!",
                    OriginalTotal = totalBookingAmount,
                    DiscountedTotal = finalTotal,
                    PointsToEarn = CalculatePointsEarned(finalTotal),
                    RemainingPoints = currentPoints - pointsToRedeem.Value
                };
            }

            // No promo or points applied
            return new LoyaltyDiscountResult
            {
                IsValid = true,
                DiscountAmount = 0m,
                PointsRequired = 0,
                PromoCode = string.Empty,
                Message = string.Empty,
                OriginalTotal = totalBookingAmount,
                DiscountedTotal = totalBookingAmount,
                PointsToEarn = pointsToEarn,
                RemainingPoints = string.IsNullOrEmpty(farmerId) ? 0 : (await _userManager.FindByIdAsync(farmerId))?.LoyaltyPoints ?? 0
            };
        }

        public async Task<(bool Success, string? Error, LoyaltyDiscountResult? Result)> RedeemPointsForBookingAsync(
            string farmerId,
            string? promoCode,
            int? pointsToRedeem,
            decimal totalBookingAmount,
            string bookingType,
            int bookingId,
            string? bookingCode)
        {
            var valResult = await ValidateAndCalculateDiscountAsync(farmerId, promoCode, pointsToRedeem, totalBookingAmount);
            if (!valResult.IsValid || valResult.DiscountAmount <= 0)
            {
                return (false, valResult.Message, valResult);
            }

            var user = await _userManager.FindByIdAsync(farmerId);
            if (user == null) return (false, "User not found.", null);

            // Deduct points from user balance if points required > 0
            if (valResult.PointsRequired > 0)
            {
                user.LoyaltyPoints = Math.Max(0, user.LoyaltyPoints - valResult.PointsRequired);
                await _userManager.UpdateAsync(user);
            }

            // Record transaction ledger entry
            var tx = new LoyaltyPointTransaction
            {
                UserId = farmerId,
                Points = -valResult.PointsRequired,
                Type = LoyaltyTransactionTypes.Redeemed,
                Description = $"Redeemed {valResult.PointsRequired} KrishiPoints for ৳{valResult.DiscountAmount:N0} discount on {bookingType} booking ({bookingCode ?? $"#{bookingId}"})",
                BookingType = bookingType,
                BookingId = bookingId,
                BookingCode = bookingCode,
                PromoCode = valResult.PromoCode,
                DiscountAmount = valResult.DiscountAmount,
                AmountSpent = valResult.DiscountedTotal,
                CreatedAt = DateTime.UtcNow
            };

            await _transactions.AddAsync(tx);
            await _transactions.SaveChangesAsync();

            return (true, null, valResult);
        }

        public async Task AwardPointsForCompletedBookingAsync(
            string farmerId,
            string bookingType,
            int bookingId,
            decimal amountSpent,
            string? bookingCode = null)
        {
            if (string.IsNullOrEmpty(farmerId)) return;

            // Prevent duplicate awards
            bool alreadyAwarded = await _transactions.Query()
                .AnyAsync(t => t.UserId == farmerId &&
                               t.BookingType == bookingType &&
                               t.BookingId == bookingId &&
                               t.Type == LoyaltyTransactionTypes.Earned);

            if (alreadyAwarded) return;

            int earnedPoints = CalculatePointsEarned(amountSpent);

            var user = await _userManager.FindByIdAsync(farmerId);
            if (user == null) return;

            user.LoyaltyPoints += earnedPoints;
            await _userManager.UpdateAsync(user);

            string codeDisplay = bookingCode ?? $"#{bookingId}";
            var tx = new LoyaltyPointTransaction
            {
                UserId = farmerId,
                Points = earnedPoints,
                Type = LoyaltyTransactionTypes.Earned,
                Description = $"Earned {earnedPoints} KrishiPoints for completed {bookingType} booking ({codeDisplay}) — ৳{amountSpent:N0} spent.",
                BookingType = bookingType,
                BookingId = bookingId,
                BookingCode = bookingCode,
                AmountSpent = amountSpent,
                CreatedAt = DateTime.UtcNow
            };

            await _transactions.AddAsync(tx);
            await _transactions.SaveChangesAsync();

            // Mark booking as points awarded
            if (bookingType.Equals("Equipment", StringComparison.OrdinalIgnoreCase))
            {
                var eqBooking = await _equipmentBookings.Query().FirstOrDefaultAsync(b => b.Id == bookingId);
                if (eqBooking != null)
                {
                    eqBooking.PointsEarned = earnedPoints;
                    eqBooking.PointsAwarded = true;
                    await _equipmentBookings.SaveChangesAsync();
                }
            }
            else if (bookingType.Equals("Godown", StringComparison.OrdinalIgnoreCase))
            {
                var gdBooking = await _godownBookings.Query().FirstOrDefaultAsync(b => b.Id == bookingId);
                if (gdBooking != null)
                {
                    gdBooking.PointsEarned = earnedPoints;
                    gdBooking.PointsAwarded = true;
                    await _godownBookings.SaveChangesAsync();
                }
            }

            // In-app notification
            await _notifications.CreateAsync(
                farmerId,
                NotificationTypes.Loyalty,
                "🎉 KrishiPoints Earned!",
                $"You earned {earnedPoints} KrishiPoints from your completed {bookingType} booking ({codeDisplay}). Redeem them for discounts on your next rental!",
                "/Loyalty"
            );
        }

        public async Task RefundPointsForCancelledBookingAsync(
            string farmerId,
            string bookingType,
            int bookingId,
            string? bookingCode = null)
        {
            if (string.IsNullOrEmpty(farmerId)) return;

            // Find redemption transaction
            var redemptionTx = await _transactions.Query()
                .FirstOrDefaultAsync(t => t.UserId == farmerId &&
                                          t.BookingType == bookingType &&
                                          t.BookingId == bookingId &&
                                          t.Type == LoyaltyTransactionTypes.Redeemed);

            if (redemptionTx == null || redemptionTx.Points >= 0) return;

            int pointsToRefund = Math.Abs(redemptionTx.Points);

            var user = await _userManager.FindByIdAsync(farmerId);
            if (user == null) return;

            user.LoyaltyPoints += pointsToRefund;
            await _userManager.UpdateAsync(user);

            string codeDisplay = bookingCode ?? $"#{bookingId}";
            var refundTx = new LoyaltyPointTransaction
            {
                UserId = farmerId,
                Points = pointsToRefund,
                Type = LoyaltyTransactionTypes.Refunded,
                Description = $"Refunded {pointsToRefund} KrishiPoints from cancelled {bookingType} booking ({codeDisplay}).",
                BookingType = bookingType,
                BookingId = bookingId,
                BookingCode = bookingCode,
                CreatedAt = DateTime.UtcNow
            };

            await _transactions.AddAsync(refundTx);
            await _transactions.SaveChangesAsync();

            await _notifications.CreateAsync(
                farmerId,
                NotificationTypes.Loyalty,
                "KrishiPoints Refunded",
                $"Your {pointsToRefund} KrishiPoints were refunded to your balance following the cancellation of booking {codeDisplay}.",
                "/Loyalty"
            );
        }

        public async Task<LoyaltyVoucherViewModel> GenerateVoucherAsync(string farmerId, int pointsTier)
        {
            var tiers = GetConversionTiers();
            var tier = tiers.FirstOrDefault(t => t.PointsRequired == pointsTier)
                       ?? throw new InvalidOperationException("Invalid points tier specified.");

            var user = await _userManager.FindByIdAsync(farmerId)
                       ?? throw new InvalidOperationException("User not found.");

            if (user.LoyaltyPoints < tier.PointsRequired)
            {
                throw new InvalidOperationException($"Insufficient points. You have {user.LoyaltyPoints} points, but {tier.PointsRequired} are required.");
            }

            // Deduct points
            user.LoyaltyPoints -= tier.PointsRequired;
            await _userManager.UpdateAsync(user);

            string randomSuffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            string voucherCode = $"{tier.PromoCodePrefix}-{randomSuffix}";

            var tx = new LoyaltyPointTransaction
            {
                UserId = farmerId,
                Points = -tier.PointsRequired,
                Type = LoyaltyTransactionTypes.VoucherGenerated,
                Description = $"Generated ৳{tier.DiscountAmount:N0} promo discount voucher ({voucherCode}) using {tier.PointsRequired} KrishiPoints.",
                PromoCode = voucherCode,
                DiscountAmount = tier.DiscountAmount,
                CreatedAt = DateTime.UtcNow
            };

            await _transactions.AddAsync(tx);
            await _transactions.SaveChangesAsync();

            return new LoyaltyVoucherViewModel
            {
                Code = voucherCode,
                DiscountAmount = tier.DiscountAmount,
                PointsCost = tier.PointsRequired,
                GeneratedAt = tx.CreatedAt,
                IsRedeemed = false
            };
        }

        private static string FormatRelativeTime(DateTime dt)
        {
            var span = DateTime.UtcNow - dt;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            return dt.ToString("dd MMM yyyy");
        }
    }
}
