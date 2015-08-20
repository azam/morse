using Livet;
using Grabacr07.KanColleViewer.Controls.Globalization;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TweetSharp;
using System.Reflection;
using System.Collections.ObjectModel;

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

        public string tags { get { return this.settings.tags; } set { this.settings.tags = value; this.settings.Save(); RaisePropertyChanged("tags"); } }

        private string _status;

        public string status
        {
            get { return this._status; }
            set
            {
                this._status = value;
                this.RaisePropertyChanged("canTweet");
                this.RaisePropertyChanged("isBusy");
                this.RaisePropertyChanged("details");
            }
        }

        public string msg
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(this.status))
                {
                    if (!string.IsNullOrWhiteSpace(this.settings.tags))
                    {
                        return string.Concat(this.status, " ", this.settings.tags);
                    }
                    else
                    {
                        return this.status;
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(this.settings.tags))
                    {
                        return this.settings.tags;
                    }
                }
                return null;
            }
        }

        private ImageSource _avatar;

        public ImageSource avatar { get { return this._avatar; } set { this._avatar = value; RaisePropertyChanged("avatar"); } }

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
                {
                    this._authorizeCommand = new RelayCommand(param => this.authorize());
                }

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
                {
                    this._verifyCommand = new RelayCommand(param => this.verify());
                }
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
                    this.updateProfile();
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
                    this.RaisePropertyChanged("details");
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
                            this.updateAvatar(u.ProfileImageUrlHttps);
                        }
                        if (!string.IsNullOrWhiteSpace(this.settings.avatar))
                        {
                            try
                            {
                                BitmapImage img = new BitmapImage();
                                img.BeginInit();
                                img.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                                img.CacheOption = BitmapCacheOption.OnLoad; // Closes stream after load
                                img.UriSource = null;
                                img.StreamSource = new MemoryStream(Convert.FromBase64String(this.settings.avatar));
                                img.EndInit();
                                img.Freeze();
                                this.avatar = (ImageSource)img;
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(e.Message, "BitmapImage failed");
                            }
                            this.RaisePropertyChanged("avatar");
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
                byte[] bytes = s.ToArray();
                string b64 = Convert.ToBase64String(bytes);
                s.Close();
                this.settings.avatar = b64;
                this.settings.Save();
            });
        }

        public void tweet()
        {
            if (this.service != null && !this.isBusy && !string.IsNullOrWhiteSpace(this.msg))
            {
                this.isBusy = true;
                this.RaisePropertyChanged("isBusy");
                this.RaisePropertyChanged("canTweet");
                Task<TwitterAsyncResult<TwitterStatus>> t = this.service.SendTweetAsync(new SendTweetOptions { Status = this.msg });
                t.ContinueWith(res =>
                {
                    this.status = "";
                    this.isBusy = false;
                    this.RaisePropertyChanged("status");
                    this.RaisePropertyChanged("isBusy");
                    this.RaisePropertyChanged("canTweet");
                    this.RaisePropertyChanged("details");
                });
            }
        }

        public ICommand _tweetCommand;

        public ICommand tweetCommand
        {
            get
            {
                if (this._tweetCommand == null)
                {
                    this._tweetCommand = new RelayCommand(param => this.tweet());
                }
                return this._tweetCommand;
            }
        }

        public string details
        {
            get
            {
                return "Length: " + this.msg + "/140 " + this.screenshotFolder;
            }
        }

        public int statusLength()
        {
            int i = this.msg.Length;
            if (this.attachments != null && this.attachments.Length > 0)
            {
                i += 23 * (this.attachments.Length) + (this.attachments.Length - 1);
            }
            return i;
        }

        public string[] attachments;

        public void clearAttachments() { }

        private string _screenshotFolder;

        public string screenshotFolder
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this._screenshotFolder))
                {
                    try
                    {
                        // Assembly[] al = AppDomain.CurrentDomain.GetAssemblies();
                        Type type = Assembly.GetEntryAssembly().GetType("Grabacr07.KanColleViewer.Models.Settings.ScreenshotSettings");
                        string dest = (string)type.GetProperty("Destination").GetValue(null, null);
                        if (string.IsNullOrWhiteSpace(dest))
                        {
                            MessageBox.Show(dest, "ScreenshotFolder");
                            this._screenshotFolder = dest;
                        }
                        this.RaisePropertyChanged("currentScreenshot");
                    }
                    catch (AmbiguousMatchException e)
                    {
                        MessageBox.Show(e.Message, "ScreenshotFolder");
                    }
                    catch (ArgumentNullException e)
                    {
                        MessageBox.Show(e.Message, "ScreenshotFolder");
                    }
                }
                return this._screenshotFolder;
            }
        }

        public ObservableCollection<string> screenshots
        {
            get
            {
                return new ObservableCollection<string>(Directory.EnumerateFiles(@"Z:\kancolle", "KanColle*.png"));
            }
        }

        private string _currentScreenshot;

        public string currentScreenshot
        {
            get
            {
                if (string.IsNullOrWhiteSpace(this._currentScreenshot))
                {
                    this._currentScreenshot = Directory.EnumerateFiles(@"Z:\kancolle", "KanColle*.png").OrderByDescending(filename => filename).First();
                }
                return this._currentScreenshot;
            }
            set
            {
                this._currentScreenshot = value;
                this.RaisePropertyChanged("currentScreenshot");
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
