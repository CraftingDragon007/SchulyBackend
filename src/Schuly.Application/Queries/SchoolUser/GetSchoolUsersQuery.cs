using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Dtos;
using Schuly.Application.Mappers;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;
using System.Security.Claims;

namespace Schuly.Application.Queries.SchoolUser
{
    [AllowAuthenticated]
    public record GetSchoolUsersQuery(Guid? ApplicationUserId = null) : IQuery<Result<List<SchoolUserDto>>>;

    public class GetSchoolUsersQueryHandler(
        SchulyDbContext dbContext,
        IAvatarUrlSigner avatarSigner,
        IUserService userService,
        IHttpContextAccessor httpContextAccessor) : IQueryHandler<GetSchoolUsersQuery, Result<List<SchoolUserDto>>>
    {
        public async ValueTask<Result<List<SchoolUserDto>>> Handle(GetSchoolUsersQuery query, CancellationToken cancellationToken)
        {
            var effectiveApplicationUserId = query.ApplicationUserId;
            if (!CanListAllSchoolUsers())
            {
                var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);
                if (effectiveApplicationUserId.HasValue && effectiveApplicationUserId.Value != currentUserId)
                    return Result<List<SchoolUserDto>>.Forbidden("Not authorized to list school users for another account.");

                effectiveApplicationUserId = currentUserId;
            }

            var dbQuery = dbContext.SchoolUsers
                .AsNoTracking()
                .AsSplitQuery()
                .Include(su => su.Absences)
                .Include(su => su.Grades)
                .Include(su => su.Classes)
                .AsQueryable();

            if (effectiveApplicationUserId.HasValue)
                dbQuery = dbQuery.Where(su => su.ApplicationUserId == effectiveApplicationUserId.Value);

            var schoolUsers = await dbQuery.ToListAsync(cancellationToken);
            var dtos = schoolUsers.ToDto();
            foreach (var dto in dtos)
                dto.ProfilePictureUrl = avatarSigner.ToPublicUrl(dto.Id, dto.ProfilePictureUrl);
            return Result<List<SchoolUserDto>>.Success(dtos);
        }

        private bool CanListAllSchoolUsers()
        {
            var roleClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<Roles>(roleClaim, out var role)
                && role is Roles.Administrator or Roles.Teacher;
        }
    }
}
