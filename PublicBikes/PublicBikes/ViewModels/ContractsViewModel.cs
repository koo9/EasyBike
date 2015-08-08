﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using PublicBikes.Config;
using PublicBikes.Design;
using PublicBikes.Models;
using PublicBikes.Notification;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Xamarin.Forms;
using System.Reflection;
using System.IO;

namespace PublicBikes.ViewModels
{
    public class ContractsViewModel : ViewModelBase
    {
        private readonly IContractService _contractsService;
        private readonly INavigationService _navigationService;
        private readonly IRefreshService _refreshService;
        private readonly IDialogService _dialogService;
        private readonly INotificationService _notificationService;
        private readonly IConfigService _configService;

        private RelayCommand<Contract> _contractTappedCommand;
        public ObservableCollection<ContractGroup> ContractGroups = new ObservableCollection<ContractGroup>();

        private int cityCounter;
        public int CityCounter
        {
            get { return cityCounter; }
            set
            {
                Set(() => CityCounter, ref cityCounter, value);
            }
        }

        [PreferredConstructor]
        public ContractsViewModel(IConfigService configService, INotificationService notificationService, IDialogService dialogService, IContractService contractsService, IRefreshService refreshService, INavigationService navigationService)
        {
            _notificationService = notificationService;
            _dialogService = dialogService;
            _navigationService = navigationService;
            _contractsService = contractsService;
            _refreshService = refreshService;
            _configService = configService;
            Init();
        }

#if DEBUG
        // This constructor is used in the Windows Phone app at design time,
        // for the Blend visual designer.

        public ContractsViewModel()
        {
            if (IsInDesignMode)
            {
                _contractsService = new DesignContractsService();
                Init();
            }
        }
#endif

        public async void Init()
        {
            var storedContracts = await _contractsService.GetContractsAsync();
            var staticContracts = _contractsService.GetStaticContracts();
            staticContracts = storedContracts.Union(staticContracts).ToList();

            var assembly = typeof(ContractsViewModel).GetTypeInfo().Assembly;
            foreach (var contract in staticContracts.GroupBy(c => c.Country).Select(c => c.First()).OrderBy(c => c.Country).ToList())
            {
                var imageMemoryStream = new MemoryStream();
                byte[] testt = null;
                try { 
                using (var stream = assembly.GetManifestResourceStream($"PublicBikes.Assets.Flags.{contract.ISO31661}.png"))
                {
                    stream.CopyTo(imageMemoryStream);
                        testt = imageMemoryStream.ToArray();
                }
                }
                catch (Exception e){

                }

                var group = new ContractGroup() { Title = contract.Country, ImageSource = testt };
                group.Items = new ObservableCollection<Contract>();
                foreach (var c in staticContracts.Where(c => c.Country == contract.Country).OrderBy(c => c.Name).ToList())
                {
                    //if (storedContracts.Any(a => a.StorageName == c.StorageName))
                    //{
                    //    c.Downloaded = true;
                    //    c.StationCounter = 
                    //}
                    CityCounter++;
                    group.Items.Add(c);
                    group.ItemsCounter++;
                }
                ContractGroups.Add(group);
            }
        }

        public RelayCommand<Contract> ContractTappedCommand
        {
            get
            {
                return _contractTappedCommand
                       ?? (_contractTappedCommand = new RelayCommand<Contract>(
                           async (contract) =>
                           {
                               if (contract.Downloading)
                               {
                                   return;
                               }
                               try
                               {
                                   if (contract.Downloaded)
                                   {
                                       await _contractsService.RemoveContractAsync(contract);
                                       contract.Downloaded = false;
                                   }
                                   else
                                   {
                                       contract.Downloading = true;
                                       var stations = await contract.GetStationsAsync();
                                       contract.Stations = stations;
                                       contract.StationCounter = stations.Count();
                                       contract.Downloaded = true;
                                       contract.Downloading = false;
                                       await _contractsService.AddContractAsync(contract);
                                   }
                               }
                               catch (Exception e)
                               {
                                   var message = $"Sorry, you are currently not able to download {contract.Name}.";
                                   await _dialogService.ShowMessage(message,
                                    "Confirmation",
                                    buttonConfirmText: "Ok", buttonCancelText: "Contact us",
                                    afterHideCallback: (confirmed) =>
                                    {
                                        if (confirmed)
                                        {
                                        }
                                        else
                                        {
                                            _notificationService.Notify(new SendEmailNotification()
                                            {
                                                Subject = $"Download city: unable to download {contract.Name}",
                                                Body = $"Hey folks ! I'm unable to download {contract.Name} with the following error: " +
                                                 $"Message: { e.Message } Inner: {e.InnerException} Stack: {e.StackTrace}",
                                            });
                                        }
                                    });
                               }
                               finally
                               {
                                   contract.Downloading = false;
                               }
                           }));
            }
        }
    }
}
