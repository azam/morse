using Livet;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TweetSharp;

namespace Morse
{
    internal class MorseViewModel : ViewModel
    {
        private ITwitterService service = null;

        private ITwitterService futureService = null;

        private OAuthRequestToken requestToken;

        private MorseSettings settings = MorseSettings.Default;

        private string _verifier;

        public string verifier { get { return this._verifier; } set { this._verifier = value; RaisePropertyChanged("canVerify"); } }

        private string _status;

        public string status { get { return this._status; } set { this._status = value; RaisePropertyChanged("canTweet"); RaisePropertyChanged("isBusy"); } }

        private BitmapImage _avatar;

        public BitmapImage avatar { get { return this._avatar; } set { this._avatar = value; RaisePropertyChanged("avatar"); } }

        public bool isSettingsOpened { get; set; }

        public bool isBusy { get; set; }

        public bool canTweet { get { return this.service != null; } }

        public bool canEditVerifier { get { return this.futureService != null && this.requestToken != null; } }

        public bool canVerify { get { return this.futureService != null && this.requestToken != null && !string.IsNullOrWhiteSpace(this.verifier); } }

        public void authorize()
        {
            this.verifier = "";
            this.requestToken = null;
            this.futureService = new TwitterService(MorseConstants.CLIENTID, MorseConstants.CLIENTSECRET);
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
                    updateProfile();
                }
                catch (Exception e)
                {
                    this.futureService = null;
                    this.requestToken = null;
                    this.verifier = "";
                    MessageBox.Show(e.Message, "Morse Plugin: " + e.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    this.RaisePropertyChanged("isSettingsOpened");
                    this.RaisePropertyChanged("canTweet");
                    this.RaisePropertyChanged("canEditVerifier");
                    this.RaisePropertyChanged("canVerify");
                }
            }
        }

        private void updateProfile()
        {
            if (this.service != null)
            {
                this.service.GetUserProfileAsync(new GetUserProfileOptions { }).ContinueWith(res =>
                {
                    TwitterUser u = res.Result.Value;
                    if (u != null)
                    {
                        if (this.settings.id != u.Id)
                        {
                            this.settings.id = u.Id;
                            this.settings.Save();
                            updateAvatar(u.ProfileImageUrlHttps);
                        }
                    }
                });
            }
        }

        private void updateAvatar(string url)
        {
            WebRequest req = WebRequest.Create(url);
            req.GetResponseAsync().ContinueWith(res =>
            {
                MemoryStream s = new MemoryStream();
                res.Result.GetResponseStream().CopyTo(s);
                this.settings.avatar = Convert.ToBase64String(s.ToArray());
                this.settings.Save();
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad; // Closes stream after load
                bi.StreamSource = s;
                bi.EndInit();
                this.avatar = bi;
                this.RaisePropertyChanged("avatar");
            });
        }

        public void tweet()
        {
            if (this.service != null && !this.isBusy && !string.IsNullOrWhiteSpace(this.status))
            {
                this.isBusy = true;
                this.RaisePropertyChanged("isBusy");
                this.RaisePropertyChanged("canTweet");
                Task<TwitterAsyncResult<TwitterStatus>> t = this.service.SendTweetAsync(new SendTweetOptions { Status = this.status });
                t.ContinueWith(res =>
                {
                    this.status = "";
                    this.isBusy = false;
                    this.RaisePropertyChanged("status");
                    this.RaisePropertyChanged("isBusy");
                    this.RaisePropertyChanged("canTweet");
                });
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

        public MorseViewModel()
        {
            this.status = "";
            this.verifier = "";
            this.requestToken = null;
            this.service = null;
            this.futureService = new TwitterService(MorseConstants.CLIENTID, MorseConstants.CLIENTSECRET);
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
