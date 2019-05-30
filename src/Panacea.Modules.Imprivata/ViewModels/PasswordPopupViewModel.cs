using Panacea.Modularity.UiManager;
using Panacea.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Panacea.Modules.Imprivata.Views;
using Panacea.Core;
using System.Windows.Input;
using Panacea.Controls;
using System.Windows.Controls;

namespace Panacea.Modules.Imprivata.ViewModels
{
    [View(typeof(PasswordPopup))]
    class PasswordPopupViewModel : PopupViewModelBase<string>
    {
        private PanaceaServices _core;
        private string Username { get; }
        private string Modality { get; }
        bool _passwordSet = false;
        public override void Deactivate()
        {
            taskCompletionSource.TrySetException(new Exception(""));
        }
        public PasswordPopupViewModel(PanaceaServices core, string username, string modality)
        {
            _core = core;
            this.Username = username;
            this.Modality = modality;
            ClickCommand = new RelayCommand((arg) =>
            {
                var PB = (arg as PasswordBox).Password;
                if (PB != "")
                {
                    _passwordSet = true;
                    SetResult(PB);
                }
            });
        }
        public ICommand ClickCommand { get; private set; }
    }
}
