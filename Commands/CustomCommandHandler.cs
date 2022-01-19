using System;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models;
using Unifiedban.Next.Models.Telegram;

namespace Unifiedban.Next.Service.Telegram.Commands;

public sealed class CustomCommandHandler
{
    public CustomCommandHandler(UBCustomCommand command, 
        QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> message)
    {
        Console.WriteLine($"Executing {command.Command}.");
            
        if (message.UBChat.EnabledCommandsType == (Enums.EnabledCommandsTypes.None | Enums.EnabledCommandsTypes.CoreOnly))
        {
            return;
        }

        if (command.TgUserLevel > 0)
        {
            if (!Program.ubTelegramUsers.ContainsKey(message.Payload.Chat.Id)) return;

            var chatUser = Program.ubTelegramUsers[message.Payload.Chat.Id]
                .FirstOrDefault(x => x.UBUser.TelegramId == message.Payload.From!.Id);
                
            if (chatUser is null) return;

            if (chatUser.UserLevel < command.TgUserLevel) return;
        }

        switch (command.AnswerType)
        {
            case UBCommand.AnswerTypes.Text:
                Program.PublishMessage(new ActionRequest()
                {
                    Action = ActionRequest.Actions.SendText,
                    Message = new ActionData(message.Payload.Chat, command.Content)
                });
                break;
            case UBCommand.AnswerTypes.Image:
                break;
            case UBCommand.AnswerTypes.Gif:
                break;
            case UBCommand.AnswerTypes.Video:
                break;
            case UBCommand.AnswerTypes.Audio:
                break;
            case UBCommand.AnswerTypes.Link:
                break;
            default:
                Program.PublishMessage(new ActionRequest()
                {
                    Action = ActionRequest.Actions.SendText,
                    Message = new ActionData(message.Payload.Chat, "Error: Unknown command answer type.")
                });
                break;
        }
    }
}