using Mediator;
using Microsoft.EntityFrameworkCore;
using Schuly.Application.Authorization;
using Schuly.Application.Models;
using Schuly.Domain.Enums;
using Schuly.Infrastructure;
using Schuly.Infrastructure.Services;

namespace Schuly.Application.Commands.SchoolUser
{
    [AllowAuthenticated]
    public record DeleteSchoolUserCommand(Guid SchoolUserId) : ICommand<Result>;

    public class DeleteSchoolUserCommandHandler(SchulyDbContext dbContext, IUserService userService) : ICommandHandler<DeleteSchoolUserCommand, Result>
    {
        public async ValueTask<Result> Handle(DeleteSchoolUserCommand command, CancellationToken cancellationToken)
        {
            var schoolUser = await dbContext.SchoolUsers
                .FirstOrDefaultAsync(su => su.Id == command.SchoolUserId, cancellationToken);

            if (schoolUser == null)
                return Result.Failure($"SchoolUser with ID '{command.SchoolUserId}' not found");

            var currentUserId = await userService.GetCurrentUserIdAsync(cancellationToken);
            if (!userService.IsCurrentUserAdmin() && schoolUser.ApplicationUserId != currentUserId)
                return Result.Failure("You can only delete your own school user.");

            dbContext.SchoolUsers.Remove(schoolUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
    }
}
