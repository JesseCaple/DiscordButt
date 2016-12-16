namespace DiscordButt
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Discord;
    using Discord.Commands;
    using Discord.Commands.Permissions;

    public class AdminPermission : IPermissionChecker
    {
        public bool CanRun(Command command, User user, Channel channel, out string error)
        {
            var role = channel.Server.Roles.Where(x => x.Name == "gods of the realm").SingleOrDefault();
            if (role != null)
            {
                error = string.Empty;
                return user.HasRole(role);
            }
            else
            {
                error = "Admin role has been renamed. Fix pls.";
                return false;
            }
        }
    }
}
