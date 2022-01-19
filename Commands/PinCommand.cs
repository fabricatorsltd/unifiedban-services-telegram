using Telegram.Bot;
using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models.Telegram;

namespace Unifiedban.Next.Service.Telegram.Commands;

[RequireBotPermissions(Enums.TelegramPermissions.CanPinMessages)]
[RequireUserPermissions(Enums.TelegramPermissions.CanPinMessages)]
public class PinCommand : Command
{
    public PinCommand(string command, QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message) :
        base(command, message)
    {
        Name = "pin";
        if (CanExecute)
            Execute(message);
        else if (message.Payload is not null && !IsDisabled)
            Program.PublishMessage(new ActionRequest()
            {
                Action = ActionRequest.Actions.SendText,
                Message = new ActionData(message.Payload.Chat, "I can't pin messages. Check permissions.")
            });
    }

    public sealed override bool Execute(QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        if (message.Payload.Text == null ||
            !message.Payload.Text.StartsWith(message.UBChat.CommandPrefix + "pin") ||
            message.Payload.ReplyToMessage is null) return false;

        try
        {
            Program.PublishMessage(new ActionRequest()
            {
                Action = ActionRequest.Actions.PinMessage,
                Message = new ActionData(message.Payload.Chat)
                {
                    ReferenceMessageId = message.Payload.ReplyToMessage.MessageId
                }
            });
            Program.PublishMessage(new ActionRequest()
            {
                Action = ActionRequest.Actions.DeleteMessage,
                Message = new ActionData(message.Payload.Chat)
                {
                    ReferenceMessageId = message.Payload.MessageId
                }
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