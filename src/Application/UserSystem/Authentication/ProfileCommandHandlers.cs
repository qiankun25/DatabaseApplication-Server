using AutoMapper;
using DbApp.Application.Common.Exceptions;
using DbApp.Application.Common.Interfaces;
using DbApp.Domain.Enums.UserSystem;
using DbApp.Domain.Services.UserSystem;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// Handler for update profile command.
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserInfoDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateProfileCommandHandler> _logger;

    public UpdateProfileCommandHandler(
        IApplicationDbContext context,
        ICacheService cacheService,
        IMapper mapper,
        ILogger<UpdateProfileCommandHandler> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<UserInfoDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get user with role information
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

            if (user == null)
            {
                throw new ValidationException("User not found.");
            }

            // Validate email uniqueness if email is being changed
            if (!string.IsNullOrEmpty(request.Email) && request.Email != user.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == request.Email && u.UserId != request.UserId, cancellationToken);
                
                if (emailExists)
                {
                    throw new ValidationException("Email address is already in use.");
                }
            }

            // Validate phone number uniqueness if phone is being changed
            if (!string.IsNullOrEmpty(request.PhoneNumber) && request.PhoneNumber != user.PhoneNumber)
            {
                var phoneExists = await _context.Users
                    .AnyAsync(u => u.PhoneNumber == request.PhoneNumber && u.UserId != request.UserId, cancellationToken);
                
                if (phoneExists)
                {
                    throw new ValidationException("Phone number is already in use.");
                }
            }

            // Update user properties
            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                user.DisplayName = request.DisplayName;
            }

            if (request.Email != null)
            {
                user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
            }

            if (request.PhoneNumber != null)
            {
                user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber;
            }

            if (request.BirthDate.HasValue)
            {
                user.BirthDate = request.BirthDate.Value;
            }

            if (request.Gender.HasValue)
            {
                user.Gender = (Gender)request.Gender.Value;
            }

            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            // Update cached session if exists
            await UpdateCachedUserInfoAsync(request.UserId, user);

            _logger.LogInformation("Profile updated for user: {UserId}", request.UserId);

            return _mapper.Map<UserInfoDto>(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {UserId}", request.UserId);
            throw;
        }
    }

    private async Task UpdateCachedUserInfoAsync(int userId, Domain.Entities.UserSystem.User user)
    {
        var sessionKey = $"user_session:{userId}";
        var session = await _cacheService.GetAsync<Domain.Models.UserSystem.Authentication.UserSession>(sessionKey);
        
        if (session != null)
        {
            // Update session with new user info if needed
            session.LastActiveTime = DateTime.UtcNow;
            await _cacheService.SetAsync(sessionKey, session, TimeSpan.FromDays(1));
        }
    }
}
