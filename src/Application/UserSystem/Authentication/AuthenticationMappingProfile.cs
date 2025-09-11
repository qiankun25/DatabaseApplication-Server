using AutoMapper;
using DbApp.Domain.Entities.UserSystem;
using DbApp.Domain.Models.UserSystem.Authentication;

namespace DbApp.Application.UserSystem.Authentication;

/// <summary>
/// AutoMapper profile for authentication-related mappings.
/// </summary>
public class AuthenticationMappingProfile : Profile
{
    public AuthenticationMappingProfile()
    {
        // User to UserInfoDto mapping
        CreateMap<User, UserInfoDto>()
            .ForMember(dest => dest.RoleName, opt => opt.MapFrom(src => src.Role.RoleName));

        // LoginHistoryEntry to LoginHistoryDto mapping
        CreateMap<LoginHistoryEntry, LoginHistoryDto>()
            .ForMember(dest => dest.SessionDurationMinutes, opt => opt.MapFrom(src => src.SessionDurationMinutes))
            .ForMember(dest => dest.IsActiveSession, opt => opt.MapFrom(src => src.IsActiveSession));

        // UserSession to UserSessionDto mapping
        CreateMap<UserSession, UserSessionDto>();

        // PasswordResetToken to anonymous object mapping (if needed)
        CreateMap<PasswordResetToken, object>()
            .ConstructUsing(src => new
            {
                src.UserId,
                src.Username,
                src.Email,
                src.CreatedAt,
                src.ExpiresAt,
                src.IsUsed,
                src.ResetMethod
            });

        // LoginFailureInfo mapping (if needed for admin purposes)
        CreateMap<LoginFailureInfo, object>()
            .ConstructUsing(src => new
            {
                src.Username,
                src.FailureCount,
                src.FirstFailureTime,
                src.LastFailureTime,
                src.LockoutUntil,
                src.IsLocked,
                FailureIpCount = src.FailureIpAddresses.Count
            });
    }
}
