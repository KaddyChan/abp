﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Volo.Abp.Domain.Repositories.MongoDB;
using Volo.Abp.MongoDB;

namespace Volo.Abp.Identity.MongoDB
{
    public class MongoIdentityUserRepository : MongoDbRepository<IAbpIdentityMongoDbContext, IdentityUser, Guid>, IIdentityUserRepository
    {
        public MongoIdentityUserRepository(IMongoDbContextProvider<IAbpIdentityMongoDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public virtual async Task<IdentityUser> FindByNormalizedUserNameAsync(
            string normalizedUserName,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable()
                .FirstOrDefaultAsync(
                    u => u.NormalizedUserName == normalizedUserName,
                    GetCancellationToken(cancellationToken)
                );
        }

        public virtual async Task<List<string>> GetRoleNamesAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var user = await GetAsync(id, cancellationToken: GetCancellationToken(cancellationToken));
            var organizationUnitIds = user.OrganizationUnits
                .Select(r => r.OrganizationUnitId)
                .ToArray();
            var organizationUnits = DbContext.OrganizationUnits
                .AsQueryable()
                .Where(ou => organizationUnitIds.Contains(ou.Id))
                .ToArray();
            var orgUnitRoleIds = organizationUnits.SelectMany(x => x.Roles.Select(r => r.RoleId)).ToArray();
            var roleIds = user.Roles.Select(r => r.RoleId).ToArray();
            var allRoleIds = orgUnitRoleIds.Union(roleIds);
            return await DbContext.Roles.AsQueryable().Where(r => allRoleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(GetCancellationToken(cancellationToken));
        }

        public async Task<List<string>> GetRoleNamesInOrganizationUnitAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var user = await GetAsync(id, cancellationToken: GetCancellationToken(cancellationToken));

            var organizationUnitIds = user.OrganizationUnits
                .Select(r => r.OrganizationUnitId)
                .ToArray();

            var organizationUnits = DbContext.OrganizationUnits
                .AsQueryable()
                .Where(ou => organizationUnitIds.Contains(ou.Id))
                .ToArray();

            var roleIds = organizationUnits.SelectMany(x => x.Roles.Select(r => r.RoleId)).ToArray();

            return await DbContext.Roles //TODO: Such usage suppress filters!
                .AsQueryable()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<IdentityUser> FindByLoginAsync(
            string loginProvider,
            string providerKey,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable()
                .Where(u => u.Logins.Any(login => login.LoginProvider == loginProvider && login.ProviderKey == providerKey))
                .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<IdentityUser> FindByNormalizedEmailAsync(
            string normalizedEmail,
            bool includeDetails = true,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable().FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<IdentityUser>> GetListByClaimAsync(
            Claim claim,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable()
                .Where(u => u.Claims.Any(c => c.ClaimType == claim.Type && c.ClaimValue == claim.Value))
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<IdentityUser>> GetListByNormalizedRoleNameAsync(
            string normalizedRoleName,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var role = await DbContext.Roles.AsQueryable().Where(x => x.NormalizedName == normalizedRoleName).FirstOrDefaultAsync(GetCancellationToken(cancellationToken));

            if (role == null)
            {
                return new List<IdentityUser>();
            }

            return await GetMongoQueryable()
                .Where(u => u.Roles.Any(r => r.RoleId == role.Id))
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<IdentityUser>> GetListAsync(
            string sorting = null,
            int maxResultCount = int.MaxValue,
            int skipCount = 0,
            string filter = null,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable()
                .WhereIf<IdentityUser, IMongoQueryable<IdentityUser>>(
                    !filter.IsNullOrWhiteSpace(),
                    u =>
                        u.UserName.Contains(filter) ||
                        u.Email.Contains(filter) ||
                        (u.Name != null && u.Name.Contains(filter)) ||
                        (u.Surname != null && u.Surname.Contains(filter))
                )
                .OrderBy(sorting ?? nameof(IdentityUser.UserName))
                .As<IMongoQueryable<IdentityUser>>()
                .PageBy<IdentityUser, IMongoQueryable<IdentityUser>>(skipCount, maxResultCount)
                .ToListAsync(GetCancellationToken(cancellationToken));
        }

        public virtual async Task<List<IdentityRole>> GetRolesAsync(
            Guid id,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var user = await GetAsync(id, cancellationToken: GetCancellationToken(cancellationToken));
            var organizationUnitIds = user.OrganizationUnits
                .Select(r => r.OrganizationUnitId)
                .ToArray();
            var organizationUnits = DbContext.OrganizationUnits
                .AsQueryable()
                .Where(ou => organizationUnitIds.Contains(ou.Id))
                .ToArray();
            var orgUnitRoleIds = organizationUnits.SelectMany(x => x.Roles.Select(r => r.RoleId)).ToArray();
            var roleIds = user.Roles.Select(r => r.RoleId).ToArray();
            var allRoleIds = orgUnitRoleIds.Union(roleIds);
            return await DbContext.Roles.AsQueryable().Where(r => allRoleIds.Contains(r.Id)).ToListAsync(GetCancellationToken(cancellationToken));
        }

        public async Task<List<OrganizationUnit>> GetOrganizationUnitsAsync(
            Guid id,
            bool includeDetails = false,
            CancellationToken cancellationToken = default)
        {
            var user = await GetAsync(id, cancellationToken: GetCancellationToken(cancellationToken));
            var organizationUnitIds = user.OrganizationUnits.Select(r => r.OrganizationUnitId);
            return await DbContext.OrganizationUnits.AsQueryable()
                            .Where(ou => organizationUnitIds.Contains(ou.Id))
                            .ToListAsync(GetCancellationToken(cancellationToken))
                            ;
        }

        public virtual async Task<long> GetCountAsync(
            string filter = null,
            CancellationToken cancellationToken = default)
        {
            return await GetMongoQueryable()
                .WhereIf<IdentityUser, IMongoQueryable<IdentityUser>>(
                    !filter.IsNullOrWhiteSpace(),
                    u =>
                        u.UserName.Contains(filter) ||
                        u.Email.Contains(filter) ||
                        (u.Name != null && u.Name.Contains(filter)) ||
                        (u.Surname != null && u.Surname.Contains(filter))
                )
                .LongCountAsync(GetCancellationToken(cancellationToken));
        }

        public async Task<List<IdentityUser>> GetUsersInOrganizationUnitAsync(
            Guid organizationUnitId,
            CancellationToken cancellationToken = default)
        {
            var result = await GetMongoQueryable()
                    .Where(u => u.OrganizationUnits.Any(uou => uou.OrganizationUnitId == organizationUnitId))
                    .ToListAsync(GetCancellationToken(cancellationToken))
                    ;
            return result;
        }

        public async Task<List<IdentityUser>> GetUsersInOrganizationsListAsync(
            List<Guid> organizationUnitIds,
            CancellationToken cancellationToken = default)
        {
            var result = await GetMongoQueryable()
                    .Where(u => u.OrganizationUnits.Any(uou => organizationUnitIds.Contains(uou.OrganizationUnitId)))
                    .ToListAsync(GetCancellationToken(cancellationToken))
                    ;
            return result;
        }

        public async Task<List<IdentityUser>> GetUsersInOrganizationUnitWithChildrenAsync(
            string code,
            CancellationToken cancellationToken = default)
        {
            var organizationUnitIds = await DbContext.OrganizationUnits.AsQueryable()
                .Where(ou => ou.Code.StartsWith(code))
                .Select(ou => ou.Id)
                .ToListAsync(GetCancellationToken(cancellationToken))
                ;

            return await GetMongoQueryable()
                     .Where(u => u.OrganizationUnits.Any(uou => organizationUnitIds.Contains(uou.OrganizationUnitId)))
                     .ToListAsync(GetCancellationToken(cancellationToken))
                     ;
        }
    }
}
