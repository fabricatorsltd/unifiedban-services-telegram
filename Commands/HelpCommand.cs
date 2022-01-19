using Telegram.Bot;
using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models.Telegram;

namespace Unifiedban.Next.Service.Telegram.Commands;

public class HelpCommand : Command
{
    public HelpCommand(string command, QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message) :
        base(command, message)
    {
        Name = "help";
        if (CanExecute && message.Payload is not null)
            Execute(message);
    }

    public sealed override bool Execute(QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        if (message.Payload.Text == null ||
            !message.Payload.Text.StartsWith(message.UBChat.CommandPrefix + "help")) return false;

        try
        {
            Program.PublishMessage(new ActionRequest()
            {
                Action = ActionRequest.Actions.SendText,
                Message = new ActionData(message.Payload.Chat, "This is help xd")
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