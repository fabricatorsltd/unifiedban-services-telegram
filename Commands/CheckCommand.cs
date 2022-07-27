using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models.Telegram;

namespace Unifiedban.Next.Service.Telegram.Commands;

public class CheckCommand : Command
{
    public CheckCommand(string command, QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message) : base(command, message)
    {
        Name = "check";
        if (CanExecute && message.Payload is not null)
            Execute(message);
    }

    public sealed override bool Execute(QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        if (message.Payload.Text == null ||
            !message.Payload.Text.StartsWith(message.UBChat.CommandPrefix + "check")) return false;

        var msg = "*Check result:*\n";
        var symbol = delegate(bool val) { return val ? "✅" : "✖"; };
        
        msg += $"_CanDeleteMessages_: {symbol(message.BotPermissions.CanDeleteMessages)}\n";
        msg += $"_CanInviteUsers_: {symbol(message.BotPermissions.CanInviteUsers)}\n";
        msg += $"_CanRestrictMembers_: {symbol(message.BotPermissions.CanRestrictMembers)}\n";
        msg += $"_CanPinMessages_: {symbol(message.BotPermissions.CanPinMessages)}";
        
        try
        {
            Program.PublishMessage(new ActionRequest()
            {
                Action = ActionRequest.Actions.SendText,
                Message = new ActionData(message.Payload.Chat, msg)
            });
            SkipChecks = true;
            return SkipChecks;
        }
        catch
        {
            // log catch reason
            return SkipChecks;
        }
    }
}