using ARH.Data;
using Microsoft.EntityFrameworkCore;

namespace ARH.Services{

    public class AuthorizationService
    {
        private readonly ArhDbContext _db;

        public AuthorizationService(ArhDbContext db)
        {
            _db = db;
        }

        public async Task<bool> IsManagerOfAsync(
            Guid managerId,
            Guid requesterId)
        {
            return await _db.Users.AnyAsync(x =>
                x.Id == requesterId &&
                x.ManagerId == managerId &&
                x.IsActive);
        }

        public async Task<bool> IsSystemOwnerOfAsync(
            Guid ownerId,
            Guid applicationId)
        {
            return await _db.Applications.AnyAsync(x =>
                x.Id == applicationId &&
                x.SystemOwnerId == ownerId &&
                x.IsActive);
        }

        public bool IsNotRequester(
            Guid actorId,
            Guid requesterId)
        {
            return actorId != requesterId;
        }
    }
}