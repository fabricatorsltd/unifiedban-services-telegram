/* unified/ban - Management and protection systems

© fabricators SRL, https://fabricators.ltd , https://unifiedban.solutions

This program is free software: you can redistribute it and/or modify
it under the terms of the fabricator's FOSS License.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the fabricator's FOSS License
along with this program.  If not, see <https://fabricators.ltd/FOSSLicense>. */

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models.Telegram;

namespace Unifiedban.Next.Service.Telegram.Commands;

public abstract class Command
{
    public string Name = string.Empty;
    public List<string> Aliases = new();
    protected readonly bool CanExecute;
    protected readonly bool IsDisabled;
    internal bool SkipChecks;

    protected Command(string command, QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        if (message.UBChat.DisabledCommands != null)
        {
            if (message.UBChat.DisabledCommands.Contains(command) ||
                message.UBChat.EnabledCommandsType ==
                (Enums.EnabledCommandsTypes.None | Enums.EnabledCommandsTypes.CustomOnly))
            {
                IsDisabled = true;
                return;
            }
        }

        var canExecAsBot = true;
        var canExecAsUser = true;
        if (Attribute.GetCustomAttribute(GetType(),
                typeof(RequireBotPermissionsAttribute)) is RequireBotPermissionsAttribute requiredBotPermission)
            canExecAsBot = requiredBotPermission.ExecuteCheck(message.BotPermissions);
        if (Attribute.GetCustomAttribute(GetType(),
                typeof(RequireUserPermissionsAttribute)) is RequireUserPermissionsAttribute requiredUserPermission)
            canExecAsUser = requiredUserPermission.ExecuteCheck(message.UserPermissions);

        CanExecute = canExecAsBot && canExecAsUser;
    }

    public virtual bool Execute(QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        SkipChecks = false;
        return SkipChecks;
    }
}