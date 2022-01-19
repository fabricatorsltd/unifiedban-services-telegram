/* unified/ban - Management and protection systems

© fabricators SRL, https://fabricators.ltd , https://unifiedban.solutions

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License with our addition
to Section 7 as published in unified/ban's the GitHub repository.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License and the
additional terms along with this program. 
If not, see <https://docs.fabricators.ltd/docs/licenses/unifiedban>.

For more information, see Licensing FAQ: 

https://docs.fabricators.ltd/docs/licenses/faq */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Telegram.Bot;
using Telegram.Bot.Types;
using Unifiedban.Next.Common;
using Unifiedban.Next.Common.Telegram;
using Unifiedban.Next.Models;
using Unifiedban.Next.Models.Telegram;
using Unifiedban.Next.Service.Telegram.Commands;

namespace Unifiedban.Next.Service.Telegram;

internal static class Program
{
    private static bool _manualShutdown;
    private static IModel _channel;
    private static IConnection? _conn;
    private static IBasicProperties _properties;

    private static Dictionary<string, Type> commands = new();
    private static Dictionary<string, Dictionary<string, UBCustomCommand>> _customCommands = new();
    internal static Dictionary<long, List<TGChatMember>> ubTelegramUsers = new();
    

    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomainOnProcessExit;

        Utils.WriteLine($"== {AppDomain.CurrentDomain.FriendlyName} Startup ==");

        var builder = new ConfigurationBuilder()
            .SetBasePath(Environment.CurrentDirectory)
            .AddJsonFile("appsettings.json", false, false);
        CacheData.Configuration = builder.Build();
        _ = new UBContext(CacheData.Configuration["Database"]);
        
        Utils.WriteLine("Registering instance");
        Utils.RegisterInstance();
        Utils.WriteLine("***************************************");
        LoadViaAbstract();
        Utils.WriteLine("***************************************");
        LoadCustomCommands();
        Utils.WriteLine("***************************************");
        LoadRabbitMQManager();
        Utils.WriteLine("***************************************");
        Utils.SetInstanceStatus(Enums.States.Operational);
        Utils.WriteLine("Startup completed.\n");

        Console.ReadLine();

        Utils.WriteLine("Manual shutdown started.\n");
        _manualShutdown = true;
        DoShutdown();
    }
    private static void LoadRabbitMQManager()
    {
        Utils.WriteLine("Creating RabbitMQ instance...");
        var factory = new ConnectionFactory();
        factory.UserName = CacheData.Configuration?["RabbitMQ:UserName"];
        factory.Password = CacheData.Configuration?["RabbitMQ:Password"];
        factory.VirtualHost = CacheData.Configuration?["RabbitMQ:VirtualHost"];
        factory.HostName = CacheData.Configuration?["RabbitMQ:HostName"];
        factory.Port = int.Parse(CacheData.Configuration?["RabbitMQ:Port"] ?? "0");
        factory.DispatchConsumersAsync = true;

        Utils.WriteLine("Connecting to RabbitMQ server...");
        _conn = factory.CreateConnection();
        _channel = _conn.CreateModel();

        _properties = _channel.CreateBasicProperties();

        var tgConsumer = new AsyncEventingBasicConsumer(_channel);
        tgConsumer.Received += ConsumerOnTgMessage;

        Utils.WriteLine("Start consuming tg.commands queue...");
        _channel.BasicConsume("tg.commands", false, tgConsumer);
    }

    private static async Task ConsumerOnTgMessage(object sender, BasicDeliverEventArgs ea)
    {
        var body = ea.Body.ToArray();
        var str = Encoding.Default.GetString(body);
        var qMessage = JsonConvert
            .DeserializeObject<QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message>>(str);

        var skipChecks = false;
        var isCustom = false;

        if (qMessage.Payload.Text.StartsWith(qMessage.UBChat.CommandPrefix))
        {
            var commandStr = qMessage.Payload.Text.Split(" ")[0];
            commandStr = commandStr.Remove(0, qMessage.UBChat.CommandPrefix.Length);
            if (commandStr.Split(' ')[0].Contains('@'))
            {
                commandStr = commandStr.Split(' ')[0].Split('@')[0];
            }
            Utils.WriteLine($"Received command: {qMessage.Payload.Text}");

            if (_customCommands.ContainsKey(qMessage.UBChat.ChatId))
            {
                if (_customCommands[qMessage.UBChat.ChatId].ContainsKey(commandStr) &&
                    _customCommands[qMessage.UBChat.ChatId][commandStr].Enabled)
                {
                    isCustom = true;
                    var customCommand = _customCommands[qMessage.UBChat.ChatId][commandStr];
                    new CustomCommandHandler(customCommand, qMessage);
                }
            }

            if (!isCustom)
            {
                var isValidCommand = commands.TryGetValue(commandStr, out var command);
                if (isValidCommand)
                    try
                    {
                        var cmdInstance = Activator.CreateInstance(command, commandStr, qMessage) as Command;
                        skipChecks = cmdInstance!.SkipChecks;
                    }
                    catch (Exception ex)
                    {
                        Utils.WriteLine($"Error executing command: {command.Name}", 3);
                        Utils.WriteLine($"Exception: {ex.Message}", 3);
                    }
                else
                    Utils.WriteLine($"Received message (starting with command token): {qMessage.Payload.Text}");
            }
        }
        else
        {
            Utils.WriteLine($"Received message: {qMessage.Payload.Text}");
        }

        if (!skipChecks)
            _channel.BasicPublish(CacheData.NextQueue.Exchange, CacheData.NextQueue.RoutingKey, _properties, body);
        _channel.BasicAck(ea.DeliveryTag, false);
    }

    private static void LoadViaAbstract()
    {
        Utils.WriteLine("Loading internal commands...");

        var type = typeof(Command);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => type.IsAssignableFrom(p) && !p.IsAbstract);

        var foundCommands = types as Type[] ?? types.ToArray();
        Utils.WriteLine($"Found {foundCommands.Count()} command(s)");

        foreach (var command in foundCommands)
        {
            var constructor = command.GetConstructor(
                new[] { typeof(string), typeof(QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message>) });
            if (constructor == null)
            {
                Utils.WriteLine($"No valid constructor found for command command: {command.Name}", 3);
                continue;
            }

            try
            {
                QueueMessage<TGChat, UserPrivileges, UserPrivileges, Message> activate = new();
                activate.UBChat = new TGChat();
                if (Activator
                        .CreateInstance(command, "", activate) is Command commandInstance)
                {
                    commands.TryAdd(commandInstance.Name, command);

                    foreach (var alias in commandInstance.Aliases) commands.TryAdd(alias, command);
                }
                else
                {
                    Utils.WriteLine($"Error creating instance for command command: {command.Name}", 3);
                }
            }
            catch (Exception ex)
            {
                Utils.WriteLine($"Error loading command: {command.Name}", 3);
                Utils.WriteLine($"Exception: {ex.Message}", 3);
            }
        }
    }
    private static void LoadCustomCommands()
    {
        Utils.WriteLine("Loading custom commands...");

        // get custom commands from db
        var customCommands = new List<UBCustomCommand>();
        customCommands.Add(new UBCustomCommand
        {
            UBCustomCommandId = "ciao",
            ChatId = "0",
            Enabled = true,
            Platforms = new string[]{"Telegram"},
            AnswerType = UBCommand.AnswerTypes.Text,
            Command = "ciao",
            Content = "Ciao un cazzo."
        });
        customCommands.Add(new UBCustomCommand
        {
            UBCustomCommandId = "ciao2",
            ChatId = "0",
            Platforms = new string[]{"Telegram"},
            AnswerType = UBCommand.AnswerTypes.Text,
            Command = "ciao2",
            Content = "Ciao un cazzo x2.",
            TgUserLevel = Enums.UserLevels.Mod
        });
        customCommands.Add(new UBCustomCommand
        {
            UBCustomCommandId = "ciao3",
            ChatId = "1",
            Platforms = new string[]{"Telegram"},
            AnswerType = UBCommand.AnswerTypes.Text,
            Command = "ciao2",
            Content = "Ciao un cazzo x3."
        });

        foreach(var c in customCommands)
        {
            if (_customCommands.ContainsKey(c.ChatId))
            {
                Utils.WriteLine($"Found command {c.Command}");
                _customCommands[c.ChatId].Add(c.Command, c);
            } 
            else 
            {
                Utils.WriteLine($"Adding commands for chat {c.ChatId}");
                Utils.WriteLine($"Found command {c.Command}");
                var d = new Dictionary<string, UBCustomCommand>();
                d.Add(c.Command, c);
                _customCommands.Add(c.ChatId, d);
            }
        }
    }
    
    private static void DoShutdown()
    {
        Utils.WriteLine("Closing RabbitMQ connection");
        _channel?.Close();
        _conn?.Close();
        Utils.WriteLine("Deregistering instance");
        Utils.DeregisterInstance();
        Utils.WriteLine("***************************************");
        Utils.WriteLine("Shutdown completed.");
    }
    private static void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = (e.ExceptionObject as Exception);
            
        Utils.WriteLine(ex?.Message);
    }
    private static void CurrentDomainOnProcessExit(object? sender, EventArgs e)
    {
        if (_manualShutdown) return;
        Utils.WriteLine("SIGTERM shutdown started.\n");
        DoShutdown();
    }
    
    internal static void PublishMessage(ActionRequest actionRequest)
    {
        if (_channel is { IsClosed: true }) return;
        
        var json = JsonConvert.SerializeObject(actionRequest);
        var body = Encoding.UTF8.GetBytes(json);
        _channel.BasicPublish("telegram", "result", _properties, body);
    }
}