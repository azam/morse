using Livet;
using MetroTrilithon.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
                string m = "";
                if (!string.IsNullOrWhiteSpace(this.status))
                {
                    m += this.status.Trim();
                }
                if (!string.IsNullOrWhiteSpace(this.settings.tags))
                {
                    if (!string.IsNullOrWhiteSpace(m))
                    {
                        m += " ";
                    }
                    m += this.settings.tags.Trim();
                }
                return m;
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
                    this.updateConfig();
                    this.updateProfile();
                    this.isSettingsOpened = false;
                    this.RaisePropertyChanged("isSettingsOpened");
                    this.RaisePropertyChanged("canTweet");
                    this.RaisePropertyChanged("canEditVerifier");
                    this.RaisePropertyChanged("canVerify");
                    this.RaisePropertyChanged("details");
                }
                catch (Exception e)
                {
                    this.futureService = null;
                    this.requestToken = null;
                    this.verifier = "";
                    this.RaisePropertyChanged("isSettingsOpened");
                    this.RaisePropertyChanged("canTweet");
                    this.RaisePropertyChanged("canEditVerifier");
                    this.RaisePropertyChanged("canVerify");
                    this.RaisePropertyChanged("details");
                    MessageBox.Show(e.Message, "Morse Plugin: " + e.GetType().ToString(), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void updateConfig()
        {
            if (this.service != null)
            {
                this.service.GetConfigurationAsync().ContinueWith(res =>
                {
                    TwitterConfiguration c = res.Result.Value;
                    this.settings.maxMedia = c.MaxMediaPerUpload;
                    this.settings.mediaChars = c.CharactersReservedPerMedia;
                    this.settings.Save();
                    this.RaisePropertyChanged("details");
                });
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
                if (this.attachments.Count > 0 && this.attachments.Count <= this.settings.maxMedia)
                {
                    List<Task<TwitterAsyncResult<TwitterUploadedMedia>>> mediaTasks = new List<Task<TwitterAsyncResult<TwitterUploadedMedia>>>();
                    foreach (string p in this.attachments)
                    {
                        mediaTasks.Add(this.service.UploadMediaAsync(new UploadMediaOptions { Media = new MediaFile { FileName = p } }));
                    }
                    Task.WhenAll(mediaTasks).ContinueWith(results =>
                    {
                        List<string> mediaIds = new List<string>();
                        foreach (TwitterAsyncResult<TwitterUploadedMedia> res in results.Result)
                        {
                            if (res.Value != null)
                            {
                                mediaIds.Add(res.Value.Media_Id);
                            }
                        }
                        this.service.SendTweetAsync(new SendTweetOptions { Status = this.msg, MediaIds = mediaIds }).ContinueWith(this.afterTweet);
                    });
                }
                else
                {
                    this.service.SendTweetAsync(new SendTweetOptions { Status = this.msg }).ContinueWith(this.afterTweet);
                }
            }
        }

        public void afterTweet(Task<TwitterAsyncResult<TwitterStatus>> task)
        {
            if (task.Result.Value != null)
            {
                this.status = "";
                this.attachments.Clear();
            }
            else
            {
                MessageBox.Show(task.Result.Response.Error.Message, "Tweet failed");
            }
            this.isBusy = false;
            this.RaisePropertyChanged("status");
            this.RaisePropertyChanged("isBusy");
            this.RaisePropertyChanged("canTweet");
            this.RaisePropertyChanged("details");
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
                return this.screenshotFolder + " " + this.statusLength + "/140";
            }
        }

        public int statusLength
        {
            get
            {
                int i = this.msg.Length;
                if (this.attachments.Count > 0)
                {
                    i += this.settings.mediaChars * this.attachments.Count + this.attachments.Count - 1;
                }
                return i;
            }
        }

        public List<string> attachments { get; set; }

        public void clearAttachments() { }

        private FileSystemWatcher watcher;

        public string screenshotExtension { get; set; }

        public string screenshotFolder { get; set; }

        public void screenshotFolderChanged()
        {
            if (this.watcher != null)
            {
                this.watcher.Dispose();
                this.watcher = null;
            }
            this.watcher = new FileSystemWatcher();
            this.watcher.Path = this.screenshotFolder;
            this.watcher.Filter = "KanColle-*" + this.screenshotExtension;
            this.watcher.NotifyFilter = NotifyFilters.FileName;
            this.watcher.Created += new FileSystemEventHandler((sender, e) => { this.screenshots.Add(e.FullPath); this.screenshots.Sort(); });
            this.watcher.Deleted += new FileSystemEventHandler((sender, e) => { this.screenshots.Remove(e.FullPath); });
            this.watcher.Renamed += new RenamedEventHandler((sender, e) =>
            {
                this.screenshots.Remove(e.OldFullPath);
                if (e.FullPath.EndsWith(this.screenshotExtension))
                {
                    this.screenshots.Add(e.FullPath);
                    this.screenshots.Sort();
                }
            });
            // this.watcher.Error += new ErrorEventHandler((sender, e) => { });
        }

        public void readScreenshotSettings()
        {
            try
            {
                Type t = Assembly.GetEntryAssembly().GetType("Grabacr07.KanColleViewer.Models.Settings.ScreenshotSettings");
                if (t != null)
                {
                    PropertyInfo pDest = t.GetProperty("Destination");
                    if (pDest != null)
                    {
                        SerializableProperty<string> spDest = (SerializableProperty<string>)pDest.GetValue(null);
                        if (spDest != null && !string.IsNullOrWhiteSpace(spDest.Value))
                        {
                            this.screenshotFolder = spDest.Value;
                        }
                    }
                    PropertyInfo pFormat = t.GetProperty("Format");
                    if (pFormat != null)
                    {
                        object spFormat = pFormat.GetValue(null);
                        if (spFormat != null)
                        {
                            PropertyInfo pValue = spFormat.GetType().GetProperty("Value");
                            if (pValue != null)
                            {
                                object f = pValue.GetValue(spFormat);
                                if (f != null)
                                {
                                    string imgFormat = System.Enum.GetName(f.GetType(), f);
                                    if (!string.IsNullOrWhiteSpace(imgFormat))
                                    {
                                        if ("Png".Equals(imgFormat))
                                        {
                                            this.screenshotExtension = ".png";
                                        }
                                        else if ("Jpeg".Equals(imgFormat))
                                        {
                                            this.screenshotExtension = ".jpg";
                                        }

                                    }
                                }
                            }
                        }
                    }
                    this.screenshots.Clear();
                    if (!string.IsNullOrWhiteSpace(this.screenshotFolder))
                    {
                        this.screenshots.AddRange(Directory.EnumerateFiles(this.screenshotFolder, "KanColle-*" + this.screenshotExtension));
                        this.screenshots.Sort();
                    }
                    this.screenshotFolderChanged();
                    this.screenshotChanged();
                }
            }
            catch (AmbiguousMatchException e)
            {
                MessageBox.Show(e.Message, "readScreenshotSettings");
            }
            catch (ArgumentNullException e)
            {
                MessageBox.Show(e.Message, "readScreenshotSettings");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "readScreenshotSettings");
            }
        }

        public List<string> screenshots { get; set; }

        public string currentScreenshot { get; set; }

        public void addScreenshot()
        {
            if (this.attachments.Count < this.settings.maxMedia && !string.IsNullOrWhiteSpace(this.currentScreenshot))
            {
                this.attachments.RemoveAll(s => this.currentScreenshot.Equals(s));
                this.attachments.Add(this.currentScreenshot);
            }
        }

        public void removeScreenshot()
        {
            if (this.attachments.Count > 0 && !string.IsNullOrWhiteSpace(this.currentScreenshot) && this.attachments.Contains(this.currentScreenshot))
            {
                this.attachments.RemoveAll(s => this.currentScreenshot.Equals(s));
            }
        }

        public bool hasNext { get { return this.screenshots.Count > 1 && !this.screenshots.Last().Equals(this.currentScreenshot); } }

        public bool hasPrevious { get { return this.screenshots.Count > 1 && !this.screenshots.First().Equals(this.currentScreenshot); } }

        public bool hasFirst { get { return this.screenshots.Count > 1 && !this.screenshots.First().Equals(this.currentScreenshot); } }

        public bool hasLast { get { return this.screenshots.Count > 1 && !this.screenshots.Last().Equals(this.currentScreenshot); } }

        public bool canAddScreenshot { get { return this.attachments.Count < this.settings.maxMedia && !string.IsNullOrWhiteSpace(this.currentScreenshot); } }

        public bool canRemoveScreenshot { get { return this.attachments.Count > 0 && !string.IsNullOrWhiteSpace(this.currentScreenshot) && this.attachments.Contains(this.currentScreenshot); } }

        public bool canToggleScreenshot { get { return this.canAddScreenshot || this.canRemoveScreenshot; } }

        public ICommand _gotoAuthorPageCommand;

        public ICommand gotoAuthorPageCommand
        {
            get
            {
                if (this._gotoAuthorPageCommand == null)
                {
                    this._gotoAuthorPageCommand = new RelayCommand(param =>
                    {
                        try
                        {
                            Process.Start(MorseResources.authorUrl);
                        }
                        catch (Exception e)
                        {
                        }
                    });
                }
                return this._gotoAuthorPageCommand;
            }
        }


        public void screenshotChanged()
        {
            this.RaisePropertyChanged("currentScreenshot");
            this.RaisePropertyChanged("hasNext");
            this.RaisePropertyChanged("hasPrevious");
            this.RaisePropertyChanged("hasLast");
            this.RaisePropertyChanged("canAddScreenshot");
            this.RaisePropertyChanged("canRemoveScreenshot");
            this.RaisePropertyChanged("canToggleScreenshot");
            this.RaisePropertyChanged("details");
        }

        public ICommand _toggleScreenshotCommand;

        public ICommand toggleScreenshotCommand
        {
            get
            {
                if (this._toggleScreenshotCommand == null)
                {
                    this._toggleScreenshotCommand = new RelayCommand(param =>
                    {
                        if (!string.IsNullOrWhiteSpace(this.currentScreenshot))
                        {
                            if (this.attachments.RemoveAll(s => this.attachments.Equals(s)) <= 0)
                            {
                                this.attachments.Add(this.currentScreenshot);
                            };
                            this.screenshotChanged();
                        }
                    });
                }
                return this._toggleScreenshotCommand;
            }
        }

        public ICommand _gotoFirstCommand;

        public ICommand gotoFirstCommand
        {
            get
            {
                if (this._gotoFirstCommand == null)
                {
                    this._gotoFirstCommand = new RelayCommand(param =>
                    {
                        if (this.screenshots.Count > 0)
                        {
                            this.currentScreenshot = this.screenshots.First();
                            this.screenshotChanged();
                        }
                    });
                }
                return this._gotoFirstCommand;
            }
        }

        public ICommand _gotoLastCommand;

        public ICommand gotoLastCommand
        {
            get
            {
                if (this._gotoLastCommand == null)
                {
                    this._gotoLastCommand = new RelayCommand(param =>
                    {
                        if (this.screenshots.Count > 0)
                        {
                            this.currentScreenshot = this.screenshots.Last();
                            this.screenshotChanged();
                        }
                    });
                }
                return this._gotoLastCommand;
            }
        }

        public ICommand _gotoNextCommand;

        public ICommand gotoNextCommand
        {
            get
            {
                if (this._gotoNextCommand == null)
                {
                    this._gotoNextCommand = new RelayCommand(param =>
                    {
                        if (this.screenshots.Count > 1)
                        {
                            int i = this.screenshots.IndexOf(this.currentScreenshot);
                            if (i + 1 < this.screenshots.Count)
                            {
                                this.currentScreenshot = this.screenshots.ElementAt(i + 1);
                                this.screenshotChanged();
                            }
                        }
                    });
                }
                return this._gotoNextCommand;
            }
        }

        public ICommand _gotoPreviousCommand;

        public ICommand gotoPreviousCommand
        {
            get
            {
                if (this._gotoPreviousCommand == null)
                {
                    this._gotoPreviousCommand = new RelayCommand(param =>
                    {
                        if (this.screenshots.Count > 1)
                        {
                            int i = this.screenshots.IndexOf(this.currentScreenshot);
                            if (i - 1 >= 0)
                            {
                                this.currentScreenshot = this.screenshots.ElementAt(i - 1);
                                this.screenshotChanged();
                            }
                        }
                    });
                }
                return this._gotoPreviousCommand;
            }
        }

        public MorseViewModel()
        {
            this.screenshots = new List<string>();
            this.screenshotExtension = ".png";
            this.screenshotFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            this.status = "";
            this.verifier = "";
            this.requestToken = null;
            this.service = null;
            this.futureService = new TwitterService(MorseConstants.CLIENTID, MorseConstants.CLIENTSECRET);
            if (this.settings != null && !string.IsNullOrWhiteSpace(this.settings.accessToken) && !string.IsNullOrWhiteSpace(this.settings.accessTokenSecret))
            {
                this.prepare(this.settings.accessToken, this.settings.accessTokenSecret);
            }
            else
            {
                this.isSettingsOpened = true;
            }
            this.readScreenshotSettings();
            // TODO: add property changed handler to KCV status service
            // this.PropertyChanged += new PropertyChangedEventHandler(this.KCVPropertyChanged);
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
