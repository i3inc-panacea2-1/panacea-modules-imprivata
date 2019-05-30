using Panacea.Core;
using Panacea.Modularity.Imprivata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Panacea.Modules.Imprivata
{
    public class ImprivataPlugin : IImprivataPlugin
    {
        //TODO: WTF?!
        private readonly string productId = "a47a5227-a83e-4b45-b2dc-353e52145134";
        private readonly PanaceaServices _core;
        public static ImprivataSettings Settings;
        ImprivataManager manager;
        public ImprivataPlugin(PanaceaServices core)
        {
            _core = core;
        }
        public Task BeginInit()
        {
            return Task.CompletedTask;
        }
        public Task EndInit()
        {
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            return;
        }
        public Task Shutdown()
        {
            return Task.CompletedTask;
        }
        public async Task<AuthenticationResult> AuthenticateCardAsync(string Code, List<string> ImprivataServers)
        {
            ImprivataSettings settings = await GetSettings();
            if (manager == null)
            {
                manager = new ImprivataManager(_core, ImprivataServers[0], productId);
            }
            return await manager.AuthenticateWithCardCodeAsync(Code, settings);
        }
        private async Task<ImprivataSettings> GetSettings()
        {
            if (Settings != null)
            {
                return Settings;
            }
            try
            {
                var res = await _core.HttpClient.GetObjectAsync<ImprivataSettings>("imprivata/get_settings/");
                Settings = res.Result;
                return Settings;
            }
            catch (Exception e) 
            {
                _core.Logger.Error(this, "error while getting imprivata settings: " + e.Message);
                return null;
            }
        }
    }

    [DataContract]
    public class ImprivataSettings
    {
        [DataMember(Name = "transform_script")]
        public string TransformScript { get; set; }
    }
}
