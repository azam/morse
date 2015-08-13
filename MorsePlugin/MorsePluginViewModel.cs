using Livet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TweetSharp;

namespace MorsePlugin
{
    internal class MorsePluginViewModel : ViewModel
    {
        private readonly string CLIENTID = "2t5mGdPSuhri5SlInpJO2GXGf";

        private readonly string CLIENTSECRET = "";

        private ITwitterService service = null;

        private ITwitterService futureService = null;

        private OAuthRequestToken requestToken;

        private MorsePluginSettings settings = MorsePluginSettings.Default;

        public string _verifier;
        public string verifier { get { return this._verifier; } set { this._verifier = value; RaisePropertyChanged("canVerify"); } }

        public string _status;
        public string status { get { return this._status; } set { this._status = value; RaisePropertyChanged("canTweet"); RaisePropertyChanged("isBusy"); } }

        public bool isSettingsOpened { get; set; }

        public bool isBusy { get; set; }

        public bool canTweet { get { return this.service != null; } }

        public bool canEditVerifier { get { return this.futureService != null && this.requestToken != null; } }

        public bool canVerify { get { return this.futureService != null && this.requestToken != null && !string.IsNullOrWhiteSpace(this.verifier); } }

        public void authorize()
        {
            this.verifier = "";
            this.requestToken = null;
            this.futureService = new TwitterService(CLIENTID, CLIENTSECRET);
            this.requestToken = this.futureService.GetRequestToken();
            try
            {
                Process.Start(this.futureService.GetAuthorizationUri(this.requestToken).ToString());
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Morse Plugin: " + e.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            this.RaisePropertyChanged("canEditVerifier");
            this.RaisePropertyChanged("canVerify");
        }

        public ICommand _authorizeCommand = null;
        public ICommand authorizeCommand
        {
            get
            {
                if (this._authorizeCommand == null)
                    this._authorizeCommand = new RelayCommand(param => this.authorize());
                return this._authorizeCommand;
            }
        }

        public void verify()
        {
            if (this.futureService != null && this.requestToken != null && !string.IsNullOrWhiteSpace(this.verifier))
            {
                OAuthAccessToken at = this.futureService.GetAccessToken(this.requestToken, this.verifier.Trim());
                this.prepare(at.Token, at.TokenSecret);
                this.isSettingsOpened = false;
            }
        }

        public ICommand _verifyCommand;
        public ICommand verifyCommand
        {
            get
            {
                if (this._verifyCommand == null)
                    this._verifyCommand = new RelayCommand(param => this.verify());
                return this._verifyCommand;
            }
        }

        private void prepare(string token, string secret)
        {
            if (this.futureService != null && !string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(secret))
            {
                try
                {
                    this.futureService.AuthenticateWith(token, secret);
                    this.service = this.futureService;
                    this.futureService = null;
                    this.requestToken = null;
                    this.verifier = "";
                    this.settings.accessToken = token;
                    this.settings.accessTokenSecret = secret;
                    this.settings.Save();
                }
                catch (Exception e)
                {
                    this.futureService = null;
                    this.requestToken = null;
                    this.verifier = "";
                    MessageBox.Show(e.Message, "Morse Plugin: " + e.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                this.RaisePropertyChanged("isSettingsOpened");
                this.RaisePropertyChanged("canTweet");
                this.RaisePropertyChanged("canEditVerifier");
                this.RaisePropertyChanged("canVerify");
            }
        }

        public void tweet()
        {
            if (this.service != null && !this.isBusy && !string.IsNullOrWhiteSpace(this.status))
            {
                this.isBusy = true;
                this.RaisePropertyChanged();
                TwitterStatus s = this.service.SendTweet(new SendTweetOptions { Status = this.status });
                this.status = "";
                this.isBusy = false;
                this.RaisePropertyChanged("isBusy");
                this.RaisePropertyChanged("canTweet");
            }
        }

        public ICommand _tweetCommand;
        public ICommand tweetCommand
        {
            get
            {
                if (this._tweetCommand == null)
                    this._tweetCommand = new RelayCommand(param => this.tweet());
                return this._tweetCommand;
            }
        }

        public MorsePluginViewModel()
        {
            this.status = "";
            this.verifier = "";
            this.requestToken = null;
            this.service = null;
            this.futureService = new TwitterService(CLIENTID, CLIENTSECRET);
            if (this.settings != null && !string.IsNullOrWhiteSpace(this.settings.accessToken) && !string.IsNullOrWhiteSpace(this.settings.accessTokenSecret))
            {
                this.prepare(this.settings.accessToken, this.settings.accessTokenSecret);
                this.isSettingsOpened = true;
            }
            else
            {
                this.isSettingsOpened = false;
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> action;

        public void Execute(object parameter)
        {
            this.action(parameter);
        }

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> action)
        {
            this.action = action;
        }
    }
}
