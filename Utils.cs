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
using System.Linq;
using System.Reflection;
using Unifiedban.Next.BusinessLogic.Log;
using Unifiedban.Next.Common;
using Unifiedban.Next.Models.Log;

namespace Unifiedban.Next.Service.Telegram;

internal class Utils
{
    private static readonly InstanceLogic _instanceLogic = new();
    private static readonly BusinessLogic.ModuleLogic _moduleLogic = new();

    internal static void RegisterInstance()
    {
        var newInstance = new Instance()
        {
            ModuleId = AppDomain.CurrentDomain.FriendlyName,
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "NoVersion",
            Start = DateTime.UtcNow,
            Status = Enums.States.Startup
        };

        var registered = _instanceLogic.Add(newInstance);
        if (registered.StatusCode != 200)
        {
            Common.Utils.WriteLine("***************************************", 4);
            Common.Utils.WriteLine("Error registering instance.", 4);
            Common.Utils.WriteLine(registered.StatusDescription, 4);
            Environment.Exit(0);
        }
            
        CacheData.Instance = registered.Payload;
        Common.Utils.WriteLine($"== InstanceId {CacheData.Instance?.InstanceId} ==");
    }
    internal static void DeregisterInstance()
    {
        if (CacheData.Instance is null)
        {
            Common.Utils.WriteLine("Trying to deregister instance but is null", 3);
            return;
        }

        CacheData.Instance.Stop = DateTime.UtcNow;
        CacheData.Instance.Status = Enums.States.Stopped;
        var updated = _instanceLogic.Update(CacheData.Instance!);
        if (updated.StatusCode == 200) return;
        Common.Utils.WriteLine("***************************************", 3);
        Common.Utils.WriteLine("Error deregistering instance.", 3);
        Common.Utils.WriteLine(updated.StatusDescription, 3);
    }
    internal static void SetInstanceStatus(Enums.States state)
    {
        if (CacheData.Instance is null)
        {
            Common.Utils.WriteLine("Trying to set instance but is null", 3);
            return;
        }
        
        CacheData.Instance.Status = state;
        var updated = _instanceLogic.Update(CacheData.Instance!);
        if (updated.StatusCode == 0) return;
    }

    internal static void GetModulesQueues()
    {
        Common.Utils.WriteLine("Getting modules queues");
        var modules = _moduleLogic.GetModules(category: Enums.QueueMessageCategories.Base).Payload;
        
        var nextQueue = modules
            .SkipWhile(x => x.ModuleId != CacheData.Instance!.ModuleId)
            .Skip(1).FirstOrDefault();
        if (nextQueue is not null)
        {
            CacheData.NextQueue = (nextQueue.Exchange, nextQueue.RoutingKey);
            Common.Utils.WriteLine($"NextQueue ({nextQueue.Exchange}, {nextQueue.RoutingKey})");
        }
    }
}