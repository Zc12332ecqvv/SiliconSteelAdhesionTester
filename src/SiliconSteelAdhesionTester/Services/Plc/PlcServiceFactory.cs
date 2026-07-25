using System;
using SiliconSteelAdhesionTester.Configuration;

namespace SiliconSteelAdhesionTester.Services.Plc
{
    internal static class PlcServiceFactory
    {
        public static IPlcService Create(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.Simulation) return new SimulationPlcService(settings);

#if SIMULATION_ONLY
            throw new InvalidOperationException(
                "当前程序是仿真构建，不能连接实体 PLC。请使用 /p:SimulationOnly=false 重新生成。");
#else
            return new S7PlcService(settings);
#endif
        }
    }
}
