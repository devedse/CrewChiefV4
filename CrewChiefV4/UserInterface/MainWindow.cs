﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrewChiefV4;
using System.Threading;
using System.IO;
using SharpDX.DirectInput;
using System.Runtime.InteropServices;
using AutoUpdaterDotNET;
using System.Net;
using System.Xml.Linq;
using System.IO.Compression;

namespace CrewChiefV4
{
    public partial class MainWindow : Form
    {
        private String driverNamesDownloadLocation = "https://www.dropbox.com/s/6mrlq93pv6uf8hi/driver_names_lo_fi_auto_updated.zip?dl=1";
        private String driverNamesTempFileName = "temp_driver_names.zip";
        private String soundPackDownloadLocation = "https://www.dropbox.com/s/zbmrq7qcefu6z6x/sounds_lo_fi_auto_updated.zip?dl=1";
        private String soundPackTempFileName = "temp_sound_pack.zip";
        private Boolean isDownloadingDriverNames = false;
        private Boolean isDownloadingSoundPack = false;
        private Boolean newSoundPackAvailable = false;
        private Boolean newDriverNamesAvailable = false;

        private ControllerConfiguration controllerConfiguration;
        
        private CrewChief crewChief;

        private Boolean isAssigningButton = false;

        private bool _IsAppRunning;

        private Boolean runListenForChannelOpenThread = false;

        private Boolean runListenForButtonPressesThread = false;

        private TimeSpan buttonCheckInterval = TimeSpan.FromMilliseconds(100);

        private VoiceOptionEnum voiceOption;

        private static String autoUpdateXMLURL = "https://www.dropbox.com/s/to5q1z5dmvfhuwm/auto_update_data.xml?dl=1";

        private float latestSoundPackVersion = 0;
        private float latestDriverNamesVersion = 0;

        private void FormMain_Load(object sender, EventArgs e)
        {
            //Uncomment below line to see Russian version

            //AutoUpdater.CurrentCulture = CultureInfo.CreateSpecificCulture("ru-RU");

            //If you want to open download page when user click on download button uncomment below line.

            //AutoUpdater.OpenDownloadPage = true;

            //Don't want user to select remind later time in AutoUpdater notification window then uncomment 3 lines below so default remind later time will be set to 2 days.

            //AutoUpdater.LetUserSelectRemindLater = false;
            //AutoUpdater.RemindLaterTimeSpan = RemindLaterFormat.Days;
            //AutoUpdater.RemindLaterAt = 2;

            AutoUpdater.Start(autoUpdateXMLURL);
            // now get the latest sound pack versions
            string xml = new WebClient().DownloadString(autoUpdateXMLURL);
            XDocument doc = XDocument.Parse(xml);
            float.TryParse(doc.Descendants("soundpackversion").First().Value, out latestSoundPackVersion);
            float.TryParse(doc.Descendants("drivernamesversion").First().Value, out latestDriverNamesVersion);
            if (latestSoundPackVersion > AudioPlayer.soundPackVersion)
            {
                downloadSoundPackButton.Enabled = true;
                downloadSoundPackButton.Text = "Updated sound pack available, press to download";
                newSoundPackAvailable = true;
            }
            if (latestDriverNamesVersion > AudioPlayer.driverNamesVersion)
            {
                downloadDriverNamesButton.Enabled = true;
                downloadDriverNamesButton.Text = "Updated driver names available, press to download";
                newDriverNamesAvailable = true;
            }
        }
        
        private void messagesVolumeSlider_Scroll(object sender, EventArgs e)
        {
            float volFloat = (float) messagesVolumeSlider.Value / 10;
            setMessagesVolume(volFloat);
            UserSettings.GetUserSettings().setProperty("messages_volume", volFloat);
            UserSettings.GetUserSettings().saveUserSettings();
        }

        private void setMessagesVolume(float vol)
        {
            int NewVolume = (int) (((float)ushort.MaxValue) * vol);
            // Set the same volume for both the left and the right channels
            uint NewVolumeAllChannels = (((uint)NewVolume & 0x0000ffff) | ((uint)NewVolume << 16));
            // Set the volume
            NativeMethods.waveOutSetVolume(IntPtr.Zero, NewVolumeAllChannels);
        }

        private void backgroundVolumeSlider_Scroll(object sender, EventArgs e)
        {
            float volFloat = (float)backgroundVolumeSlider.Value / 10;
            UserSettings.GetUserSettings().setProperty("background_volume", volFloat);
            UserSettings.GetUserSettings().saveUserSettings();
        }
        
        public bool IsAppRunning
        {
            get
            {
                return _IsAppRunning;
            }
            set
            {
                _IsAppRunning = value;
                startApplicationButton.Text = _IsAppRunning ? "Stop" : "Start Application";
                downloadDriverNamesButton.Enabled = !value && newDriverNamesAvailable;
                downloadSoundPackButton.Enabled = !value && newSoundPackAvailable;
            }
        }

        private void setSelectedGameType()
        {
            String[] commandLineArgs = Environment.GetCommandLineArgs();
            Boolean setFromCommandLine = false;
            if (commandLineArgs != null)
            {
                foreach (String arg in commandLineArgs)
                {
                    if (arg.Equals(GameDefinition.raceRoom.gameEnum.ToString()))
                    {
                        this.gameDefinitionList.Text = GameDefinition.raceRoom.friendlyName;
                        setFromCommandLine = true;
                        break;
                    }
                    else if (arg.Equals(GameDefinition.pCars32Bit.gameEnum.ToString()))
                    {
                        this.gameDefinitionList.Text = GameDefinition.pCars32Bit.friendlyName;
                        setFromCommandLine = true;
                        break;
                    }
                    else if (arg.Equals(GameDefinition.pCars64Bit.gameEnum.ToString()))
                    {
                        this.gameDefinitionList.Text = GameDefinition.pCars64Bit.friendlyName;
                        setFromCommandLine = true;
                        break;
                    }
                    else if (arg.Equals(GameDefinition.pCarsNetwork.gameEnum.ToString()))
                    {
                        this.gameDefinitionList.Text = GameDefinition.pCarsNetwork.friendlyName;
                        setFromCommandLine = true;
                        break;
                    }
                }
            }
            if (!setFromCommandLine)
            {
                String lastDef = UserSettings.GetUserSettings().getString("last_game_definition");
                if (lastDef != null && lastDef.Length > 0)
                {
                    GameDefinition gameDefinition = GameDefinition.getGameDefinitionForEnumName(lastDef);
                    if (gameDefinition != null)
                    {
                        this.gameDefinitionList.Text = gameDefinition.friendlyName;
                    }
                }
            }
        }

        private void updateSelectedGame()
        {

        }

        public MainWindow()
        {
            controllerConfiguration = new ControllerConfiguration();
            InitializeComponent();
            setSelectedGameType();
            this.app_version.Text = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            this.filenameLabel.Visible = System.Diagnostics.Debugger.IsAttached;
            this.filenameTextbox.Visible = System.Diagnostics.Debugger.IsAttached;
            this.recordSession.Visible = System.Diagnostics.Debugger.IsAttached;
            this.playbackInterval.Visible = System.Diagnostics.Debugger.IsAttached;
            CheckForIllegalCrossThreadCalls = false;
            Console.SetOut(new ControlWriter(textBox1));
            crewChief = new CrewChief();
            float messagesVolume = UserSettings.GetUserSettings().getFloat("messages_volume");
            float backgroundVolume = UserSettings.GetUserSettings().getFloat("background_volume");
            setMessagesVolume(messagesVolume);
            messagesVolumeSlider.Value = (int)(messagesVolume * 10f);
            backgroundVolumeSlider.Value = (int) (backgroundVolume * 10f);

            getControllers();
            controllerConfiguration.loadSettings(this);
            String customDeviceGuid = UserSettings.GetUserSettings().getString("custom_device_guid");
            if (customDeviceGuid != null && customDeviceGuid.Length > 0)
            {
                try
                {
                    Guid guid;
                    if (Guid.TryParse(customDeviceGuid, out guid)) {
                        controllerConfiguration.addCustomController(guid);
                    }
                    else
                    {
                        Console.WriteLine("Failed to add custom device, unable to process GUID");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to add custom device, message: " + e.Message);
                }
            }
            voiceOption = getVoiceOptionEnum(UserSettings.GetUserSettings().getString("VOICE_OPTION"));
            if (voiceOption == VoiceOptionEnum.DISABLED)
            {
                this.voiceDisableButton.Checked = true;
            }
            else if (voiceOption == VoiceOptionEnum.ALWAYS_ON)
            {
                this.alwaysOnButton.Checked = true;
            } else if (voiceOption == VoiceOptionEnum.HOLD)
            {
                this.holdButton.Checked = true;
            }
            else if (voiceOption == VoiceOptionEnum.TOGGLE)
            {
                this.toggleButton.Checked = true;
            }
            if (voiceOption != VoiceOptionEnum.DISABLED)
            {
                initialiseSpeechEngine();
            }
            runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
            updateActions();
            this.assignButtonToAction.Enabled = false;
            this.deleteAssigmentButton.Enabled = false;

            if (UserSettings.GetUserSettings().getBoolean("run_immediately") &&
                GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Text) != null)
            {
                doStartAppStuff();
            }
        }

        private void listenForChannelOpen()
        {
            Boolean channelOpen = false;
            if (crewChief.speechRecogniser.initialised && voiceOption == VoiceOptionEnum.HOLD)
            {
                Console.WriteLine("Running speech recognition in 'hold button' mode");
                crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.HOLD;
                while (runListenForChannelOpenThread)
                {
                    Thread.Sleep(100);
                    if (!channelOpen && controllerConfiguration.isChannelOpen())
                    {
                        channelOpen = true;
                        crewChief.audioPlayer.playStartListeningBeep();
                        crewChief.speechRecogniser.recognizeAsync();
                        Console.WriteLine("Listening...");
                    }
                    else if (channelOpen && !controllerConfiguration.isChannelOpen())
                    {
                        Console.WriteLine("Stopping listening...");
                        crewChief.speechRecogniser.recognizeAsyncCancel();
                        channelOpen = false;
                    }
                }        
            }            
        }

        private void listenForButtons()
        {
            DateTime lastButtoncheck = DateTime.Now;
            Boolean channelOpen = false;
            if (crewChief.speechRecogniser.initialised && voiceOption == VoiceOptionEnum.TOGGLE) 
            {
                Console.WriteLine("Running speech recognition in 'toggle button' mode");
            }
            while (runListenForButtonPressesThread)
            {
                Thread.Sleep(100);
                DateTime now = DateTime.Now;
                controllerConfiguration.pollForButtonClicks(voiceOption == VoiceOptionEnum.TOGGLE);
                int nextPollWait = 0;
                if (now > lastButtoncheck.Add(buttonCheckInterval))
                {
                    lastButtoncheck = now;
                    if (controllerConfiguration.hasOutstandingClick(ControllerConfiguration.TOGGLE_RACE_UPDATES_FUNCTION))
                    {
                        Console.WriteLine("Toggling keep quiet mode");
                        crewChief.toggleKeepQuietMode();
                        nextPollWait = 1000;
                    }
                    else if (controllerConfiguration.hasOutstandingClick(ControllerConfiguration.TOGGLE_SPOTTER_FUNCTION))
                    {
                        Console.WriteLine("Toggling spotter mode");
                        crewChief.toggleSpotterMode();
                        nextPollWait = 1000;
                    }
                    else if (controllerConfiguration.hasOutstandingClick(ControllerConfiguration.TOGGLE_READ_OPPONENT_DELTAS))
                    {
                        Console.WriteLine("Toggling read opponent deltas mode");
                        crewChief.toggleReadOpponentDeltasMode();
                        nextPollWait = 1000;
                    }
                    else if (controllerConfiguration.hasOutstandingClick(ControllerConfiguration.REPEAT_LAST_MESSAGE_BUTTON))
                    {
                        Console.WriteLine("Repeating last message");
                        crewChief.audioPlayer.repeatLastMessage();
                        nextPollWait = 1000;
                    }
                    else if (crewChief.speechRecogniser.initialised && voiceOption == VoiceOptionEnum.TOGGLE && 
                        controllerConfiguration.hasOutstandingClick(ControllerConfiguration.CHANNEL_OPEN_FUNCTION))
                    {
                        crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.TOGGLE;
                        if (!channelOpen)
                        {
                            Console.WriteLine("Listening...");
                            channelOpen = true;
                            crewChief.speechRecogniser.recognizeAsync();
                        }
                        else
                        {
                            Console.WriteLine("Finished listening...");
                            channelOpen = false;
                            crewChief.speechRecogniser.recognizeAsyncCancel();
                        }
                        nextPollWait = 1000;
                    }
                }
                Thread.Sleep(nextPollWait);
            }
        }
        
        private void startApplicationButton_Click(object sender, EventArgs e)
        {
            doStartAppStuff();
        }

        private void doStartAppStuff()
        {
            IsAppRunning = !IsAppRunning;
            if (_IsAppRunning)
            {
                GameDefinition gameDefinition = GameDefinition.getGameDefinitionForFriendlyName(gameDefinitionList.Text);
                if (gameDefinition != null)
                {
                    crewChief.setGameDefinition(gameDefinition);
                }
                else
                {
                    MessageBox.Show("Please choose a game option", "No game selected",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                this.assignButtonToAction.Enabled = false;
                this.deleteAssigmentButton.Enabled = false;
                this.groupBox1.Enabled = false;
                this.button1.Enabled = false;
                ThreadStart crewChiefWork = runApp;
                Thread crewChiefThread = new Thread(crewChiefWork);
                crewChiefThread.Start();
                runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen()
                    && voiceOption == VoiceOptionEnum.HOLD && crewChief.speechRecogniser.initialised;
                if (runListenForChannelOpenThread && voiceOption == VoiceOptionEnum.HOLD && crewChief.speechRecogniser.initialised)
                {
                    Console.WriteLine("Listening on default audio input device");
                    ThreadStart channelOpenButtonListenerWork = listenForChannelOpen;
                    Thread channelOpenButtonListenerThread = new Thread(channelOpenButtonListenerWork);
                    channelOpenButtonListenerThread.Start();
                }
                else if (voiceOption == VoiceOptionEnum.ALWAYS_ON && crewChief.speechRecogniser.initialised)
                {
                    Console.WriteLine("Running speech recognition in 'always on' mode");
                    crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.ALWAYS_ON;
                    crewChief.speechRecogniser.recognizeAsync();
                }
                if (runListenForButtonPressesThread)
                {
                    Console.WriteLine("Listening for buttons");
                    ThreadStart buttonPressesListenerWork = listenForButtons;
                    Thread buttonPressesListenerThread = new Thread(buttonPressesListenerWork);
                    buttonPressesListenerThread.Start();
                }
            }
            else
            {
                if (voiceOption == VoiceOptionEnum.ALWAYS_ON && crewChief.speechRecogniser.initialised)
                {
                    Console.WriteLine("Stopping listening...");
                    crewChief.speechRecogniser.recognizeAsyncCancel();
                }
                this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 &&
                    this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].joystick != null;
                this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1;
                stopApp();
                Console.WriteLine("Application stopped");
                this.button1.Enabled = true;
                this.groupBox1.Enabled = true;
            }
        }

        private void stopApp(object sender, FormClosedEventArgs e)
        {
            stopApp();
        }

        private void runApp()
        {
            String filenameToRun = null;
            int interval = 0;
            Boolean record = false;
            if (System.Diagnostics.Debugger.IsAttached && filenameTextbox.Text != null && filenameTextbox.Text.Count() > 0)
            {
                filenameToRun = filenameTextbox.Text;
                if (playbackInterval.Text.Length > 0)
                {
                    interval = int.Parse(playbackInterval.Text);
                }
            }
            if (System.Diagnostics.Debugger.IsAttached && recordSession.Checked) {
                record = true;
            }
            if (!crewChief.Run(filenameToRun, interval, record))
            {
                this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 &&
                    this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].joystick != null;
                this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1;
                stopApp();
                this.button1.Enabled = true;
                IsAppRunning = false;
            }
        }
        
        private void stopApp()
        {
            runListenForChannelOpenThread = false;
            runListenForButtonPressesThread = false;
            crewChief.stop();
        }

        private void buttonActionSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 && !crewChief.running;
            this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && !crewChief.running;
        }

        private void controllersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.deleteAssigmentButton.Enabled = this.buttonActionSelect.SelectedIndex > -1 && !crewChief.running;
            this.assignButtonToAction.Enabled = this.buttonActionSelect.SelectedIndex > -1 && this.controllersList.SelectedIndex > -1 && !crewChief.running;
        }

        private void getControllers() {
            this.controllersList.Items.Clear();
            foreach (ControllerConfiguration.ControllerData configData in controllerConfiguration.controllers)
            {
                this.controllersList.Items.Add(configData.deviceType.ToString() + " " + configData.deviceName);
            }
        }

        private void updateActions()
        {
            this.buttonActionSelect.Items.Clear();
            foreach (ControllerConfiguration.ButtonAssignment assignment in controllerConfiguration.buttonAssignments)
            {
                this.buttonActionSelect.Items.Add(assignment.getInfo());
            }
        }

        private void assignButtonToActionClick(object sender, EventArgs e)
        {
            if (!isAssigningButton)
            {
                if (this.controllersList.SelectedIndex >= 0 && this.buttonActionSelect.SelectedIndex >= 0)
                {
                    isAssigningButton = true;
                    this.assignButtonToAction.Text = "Waiting for button, click to cancel";
                    ThreadStart assignButtonWork = assignButton;
                    Thread assignButtonThread = new Thread(assignButtonWork);
                    assignButtonThread.Start();
                }                
            }
            else
            {
                isAssigningButton = false;
                controllerConfiguration.listenForAssignment = false;
                this.assignButtonToAction.Text = "Assign";
            }
        }

        private void initialiseSpeechEngine()
        {
            try
            {
                if (!crewChief.speechRecogniser.initialised)
                {
                    crewChief.speechRecogniser.initialiseSpeechEngine();
                    Console.WriteLine("Attempted to initialise speech engine - success = " + crewChief.speechRecogniser.initialised);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create speech engine, error message: " + e.Message);
                runListenForChannelOpenThread = false;
            }
        }

        private void assignButton()
        {
            if (controllerConfiguration.assignButton(this, this.controllersList.SelectedIndex, this.buttonActionSelect.SelectedIndex))
            {
                updateActions();
                isAssigningButton = false;
                controllerConfiguration.saveSettings();
                runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen() && voiceOption != VoiceOptionEnum.DISABLED;
                if (runListenForChannelOpenThread)
                {
                    initialiseSpeechEngine();
                }
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
            }
            this.assignButtonToAction.Text = "Assign";
            controllerConfiguration.saveSettings();
        }

        private void deleteAssignmentButtonClicked(object sender, EventArgs e)
        {
            if (this.buttonActionSelect.SelectedIndex >= 0)
            {
                this.controllerConfiguration.buttonAssignments[this.buttonActionSelect.SelectedIndex].unassign();                
                updateActions();
                runListenForChannelOpenThread = controllerConfiguration.listenForChannelOpen();
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(voiceOption == VoiceOptionEnum.TOGGLE);
            }
            controllerConfiguration.saveSettings();
        }

        private void editPropertiesButtonClicked(object sender, EventArgs e)
        {
            var form = new PropertiesForm(this);
            form.ShowDialog(this);
        }

        private void voiceDisableButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                runListenForChannelOpenThread = false;
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                voiceOption = VoiceOptionEnum.DISABLED;
                UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                UserSettings.GetUserSettings().saveUserSettings();
            }            
        }
        private void holdButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                try
                {
                    initialiseSpeechEngine();
                    crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.HOLD;
                    voiceOption = VoiceOptionEnum.HOLD;
                    runListenForChannelOpenThread = true;
                    UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                    UserSettings.GetUserSettings().saveUserSettings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }  
            }            
        }
        private void toggleButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                runListenForButtonPressesThread = true;
                runListenForChannelOpenThread = false;
                try
                {
                    initialiseSpeechEngine();
                    crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.TOGGLE;
                    voiceOption = VoiceOptionEnum.TOGGLE;
                    UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                    UserSettings.GetUserSettings().saveUserSettings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }  
            }
        }
        private void alwaysOnButton_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                runListenForChannelOpenThread = false;
                runListenForButtonPressesThread = controllerConfiguration.listenForButtons(false);
                try
                {
                    initialiseSpeechEngine();
                    crewChief.speechRecogniser.voiceOptionEnum = VoiceOptionEnum.ALWAYS_ON;
                    voiceOption = VoiceOptionEnum.ALWAYS_ON;
                    UserSettings.GetUserSettings().setProperty("VOICE_OPTION", getVoiceOptionString());
                    UserSettings.GetUserSettings().saveUserSettings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to initialise speech engine, message = " + ex.Message);
                }                
            }
        }
            
        private VoiceOptionEnum getVoiceOptionEnum(String enumStr)
        {
            VoiceOptionEnum enumVal = VoiceOptionEnum.DISABLED;
            if (enumStr != null && enumStr.Length > 0) {
                 enumVal = (VoiceOptionEnum)VoiceOptionEnum.Parse(typeof(VoiceOptionEnum), enumStr, true);
            }
            return enumVal;
        }

        private String getVoiceOptionString()
        {
            return voiceOption.ToString();
        }

        public enum VoiceOptionEnum
        {
            DISABLED, HOLD, TOGGLE, ALWAYS_ON
        }
        
        private void clearConsole(object sender, EventArgs e)
        {
            if (!textBox1.IsDisposed)
            {
                try
                {
                    textBox1.Text = "";
                }
                catch (Exception)
                {
                    // swallow - nothing to log it to
                }
            }
        }

        private void updateSelectedGameDefinition(object sender, EventArgs e)
        {
            if (this.gameDefinitionList.Text.Equals(GameDefinition.pCarsNetwork.friendlyName))
            {
                controllerConfiguration.addNetworkControllerToList();
            }
            else
            {
                controllerConfiguration.removeNetworkControllerFromList();                
            }
            getControllers();
        }  

        private void startDownload(Boolean isSoundPack)
        {
            using (WebClient wc = new WebClient())
            {
                if (isSoundPack)
                {
                    isDownloadingSoundPack = true;
                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(soundpack_DownloadProgressChanged);
                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(soundpack_DownloadFileCompleted);
                    wc.DownloadFileAsync(new Uri(soundPackDownloadLocation), 
                        AudioPlayer.soundFilesPath + "/" + soundPackTempFileName);
                }
                else
                {
                    isDownloadingDriverNames = true;
                    wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(drivernames_DownloadProgressChanged);
                    wc.DownloadFileCompleted += new AsyncCompletedEventHandler(drivernames_DownloadFileCompleted);
                    wc.DownloadFileAsync(new Uri(driverNamesDownloadLocation),
                        AudioPlayer.soundFilesPath + "/" + driverNamesTempFileName);
                }
            }
        }

        void soundpack_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            soundPackProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
        }

        void drivernames_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            driverNamesProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
        }
        void soundpack_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadSoundPackButton.Text = "Extracting sound pack...";
            ZipFile.ExtractToDirectory(AudioPlayer.soundFilesPath + "/" + soundPackTempFileName, AudioPlayer.soundFilesPath);
            downloadSoundPackButton.Text = "Sound pack is up to date";
            soundPackProgressBar.Value = 0;
            isDownloadingSoundPack = false;
            File.Delete(AudioPlayer.soundFilesPath + "/" + soundPackTempFileName);
            if (!isDownloadingDriverNames)
            {
                doRestart();        
            }
        }
        void drivernames_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadSoundPackButton.Text = "Extracting driver names...";
            ZipFile.ExtractToDirectory(AudioPlayer.soundFilesPath + "/" + driverNamesTempFileName, AudioPlayer.soundFilesPath);
            downloadDriverNamesButton.Text = "Driver names are up to date";
            driverNamesProgressBar.Value = 0;
            isDownloadingDriverNames = false;
            File.Delete(AudioPlayer.soundFilesPath + "/" + driverNamesTempFileName);
            if (!isDownloadingSoundPack)
            {
                doRestart();             
            }
        }

        private void doRestart()
        {
            String warningMessage = "The application must be restarted to load the new sounds. Click OK to restart the application.";
            if (System.Diagnostics.Debugger.IsAttached)
            {
                warningMessage = "The app must be restarted manually to load the new sounds";
            }
            if (MessageBox.Show(warningMessage, "Save changes", MessageBoxButtons.OK) == DialogResult.OK)
            {
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    System.Diagnostics.Process.Start(Application.ExecutablePath, String.Join(" ", Environment.GetCommandLineArgs())); // to start new instance of application
                    this.Close(); //to turn off current app
                }
            }   
        }

        private void downloadSoundPackButtonPress(object sender, EventArgs e)
        {
            startApplicationButton.Enabled = false;
            downloadSoundPackButton.Text = "Downloading sound pack...";
            downloadSoundPackButton.Enabled = false;
            startDownload(true);

        }
        private void downloadDriverNamesButtonPress(object sender, EventArgs e)
        {
            startApplicationButton.Enabled = false;
            downloadDriverNamesButton.Text = "Downloading driver names...";
            downloadDriverNamesButton.Enabled = false;
            startDownload(false);
        }
    }

    public class ControlWriter : TextWriter
    {
        private TextBox textbox;
        public ControlWriter(TextBox textbox)
        {
            this.textbox = textbox;
        }

        public override void Write(char value)
        {
            if (!textbox.IsDisposed)
            {
                textbox.AppendText(value.ToString());
            }
        }

        public override void Write(string value)
        {
            if (!textbox.IsDisposed)
            {
                textbox.AppendText(value);
            }
        }

        public override void WriteLine(string value)
        {
            if (!textbox.IsDisposed)
            {
                try
                {
                    textbox.AppendText(DateTime.Now.ToString("HH:mm:ss.fff"));
                    textbox.AppendText(" : ");
                    textbox.AppendText(value + "\n");
                }
                catch (Exception)
                {
                    // swallow - nothing to log it to
                }
            }
        }

        public override Encoding Encoding
        {
            get { return Encoding.ASCII; }
        }
    }

    static class NativeMethods
    {
        [DllImport("winmm.dll")]
        public static extern int waveOutGetVolume(IntPtr hwo, out uint dwVolume);

        [DllImport("winmm.dll")]
        public static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

    }
}