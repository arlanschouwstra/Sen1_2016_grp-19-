// Xamarin/C# app voor de besturing van een Arduino (Uno met Ethernet Shield) m.b.v. een socket-interface.
// Dit programma werkt samen met het Arduino-programma DomoticaServer.ino
// De besturing heeft betrekking op het aan- en uitschakelen van een Arduino pin, waar een led aan kan hangen of, 
// t.b.v. het Domotica project, een RF-zender waarmee een klik-aan-klik-uit apparaat bestuurd kan worden.
//
// De app heeft twee modes die betrekking hebben op de afhandeling van de socket-communicatie: "simple-mode" en "threaded-mode" 
// Wanneer het statement    //connector = new Connector(this);    wordt uitgecommentarieerd draait de app in "simple-mode",
// Het opvragen van gegevens van de Arduino (server) wordt dan met een Timer gerealisseerd. (De extra classes Connector.cs, 
// Receiver.cs en Sender.cs worden dan niet gebruikt.) 
// Als er een connector wordt aangemaakt draait de app in "threaded mode". De socket-communicatie wordt dan afgehandeld
// via een Sender- en een Receiver klasse, die worden aangemaakt in de Connector klasse. Deze threaded mode 
// biedt een generiekere en ook robuustere manier van communicatie, maar is ook moeilijker te begrijpen. 
// Aanbeveling: start in ieder geval met de simple-mode
//
// Werking: De communicatie met de (Arduino) server is gebaseerd op een socket-interface. Het IP- en Port-nummer
// is instelbaar. Na verbinding kunnen, middels een eenvoudig commando-protocol, opdrachten gegeven worden aan 
// de server (bijv. pin aan/uit). Indien de server om een response wordt gevraagd (bijv. led-status of een
// sensorwaarde), wordt deze in een 4-bytes ASCII-buffer ontvangen, en op het scherm geplaatst. Alle commando's naar 
// de server zijn gecodeerd met 1 char.
//
// Aanbeveling: Bestudeer het protocol in samenhang met de code van de Arduino server.
// Het default IP- en Port-nummer (zoals dat in het GUI verschijnt) kan aangepast worden in de file "Strings.xml". De
// ingestelde waarde is gebaseerd op je eigen netwerkomgeving, hier, en in de Arduino-code, is dat een router, die via DHCP
// in het segment 192.168.1.x IP-adressen uitgeeft.
// 
// Resource files:
//   Main.axml (voor het grafisch design, in de map Resources->layout)
//   Strings.xml (voor alle statische strings in het interface (ook het default IP-adres), in de map Resources->values)
// 
// De software is verder gedocumenteerd in de code. Tijdens de colleges wordt er nadere uitleg over gegeven.
// 
// Versie 1.2, 16/12/2016
// S. Oosterhaven
// W. Dalof (voor de basis van het Threaded interface)
//
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Android.Graphics;
using System.Threading.Tasks;
using Mono;


namespace Domotica
{
    [Activity(Label = "SmartLight", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait,  ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        Theme = "@style/Theme.Custom")]

    public class MainActivity : Activity
    {
        
      
        // Variables (components/controls)
        // Controls on GUI
        Button buttonConnect;
        Button buttonChangePinState;
        Button buttonChangePinState1;
        Button buttonChangePinState2;
        Button btnTimer;
        Button SensorButton;
        TextView textViewServerConnect, textViewTimerStateValue, textViewCountDownTimer, textViewTemperature;
        public TextView textViewChangePinStateValue, textViewSensorValue, textViewChangePinStateValue1, textViewChangePinStateValue2;
        EditText editTextIPAddress, editTextIPPort, editTextSensorWaarde, editTextTimerWaarde;

        public Timer timerClock, timerSockets, CountDownTimer;             // Timers
        
        Socket socket = null;                       // Socket   
        Connector connector = null;                 // Connector (simple-mode or threaded-mode)
        Connector connector1 = null;
        Connector connector2 = null;
        List<Tuple<string, TextView>> commandList = new List<Tuple<string, TextView>>();  // List for commands and response places on UI
        int listIndex = 0;
        int countTime;
        int SensorWaarde;
        int SensorWaarde2;

        protected override void OnCreate(Bundle bundle)
        {

            base.OnCreate(bundle);

            // Set our view from the "main" layout resource (strings are loaded from Recources -> values -> Strings.xml)
            SetContentView(Resource.Layout.Main);

            // find and set the controls, so it can be used in the code
            buttonConnect = FindViewById<Button>(Resource.Id.buttonConnect);
            buttonChangePinState = FindViewById<Button>(Resource.Id.buttonChangePinState);
            buttonChangePinState1 = FindViewById<Button>(Resource.Id.buttonChangePinState1);
            buttonChangePinState2 = FindViewById<Button>(Resource.Id.buttonChangePinState2);
            btnTimer = FindViewById<Button>(Resource.Id.btnTimer);
            textViewTimerStateValue = FindViewById<TextView>(Resource.Id.textViewTimerStateValue);
            textViewCountDownTimer = FindViewById<TextView>(Resource.Id.textViewCountDownTimer);
            textViewServerConnect = FindViewById<TextView>(Resource.Id.textViewServerConnect);
            textViewChangePinStateValue = FindViewById<TextView>(Resource.Id.textViewChangePinStateValue);
            textViewChangePinStateValue1 = FindViewById<TextView>(Resource.Id.textViewChangePinStateValue1);
            textViewChangePinStateValue2 = FindViewById<TextView>(Resource.Id.textViewChangePinStateValue2);
            textViewSensorValue = FindViewById<TextView>(Resource.Id.textViewSensorValue);
            editTextIPAddress = FindViewById<EditText>(Resource.Id.editTextIPAddress);
            editTextIPPort = FindViewById<EditText>(Resource.Id.editTextIPPort);
            editTextSensorWaarde = FindViewById<EditText>(Resource.Id.editTextSensorWaarde);
            editTextTimerWaarde = FindViewById<EditText>(Resource.Id.editTextTimerWaarde);
            SensorButton = FindViewById<Button>(Resource.Id.SensorButton);
            textViewTemperature = FindViewById<TextView>(Resource.Id.textViewTemperature);

            UpdateConnectionState(4, "Disconnected");

            // Init commandlist, scheduled by socket timer
            commandList.Add(new Tuple<string, TextView>("s", textViewChangePinStateValue));
            commandList.Add(new Tuple<string, TextView>("b", textViewChangePinStateValue1));
            commandList.Add(new Tuple<string, TextView>("c", textViewChangePinStateValue2));
            commandList.Add(new Tuple<string, TextView>("a", textViewSensorValue));
            commandList.Add(new Tuple<string, TextView>("x", textViewTemperature));
            // activation of connector -> threaded sockets otherwise -> simple sockets 
            // connector = new Connector(this);

            this.Title = (connector == null) ? this.Title + " (simple sockets)" : this.Title + " (thread sockets)";
            this.Title = (connector1 == null) ? this.Title + " (simple sockets)" : this.Title + " (thread sockets)";

            

            timerClock = new System.Timers.Timer() { Interval = 2000, Enabled = true }; // Interval >= 1000
            timerClock.Elapsed += (obj, args) =>
            {
                RunOnUiThread(() => { textViewTimerStateValue.Text = DateTime.Now.ToString("h:mm:ss"); }); 
            };

          

            // timer object, check Arduino state
            // Only one command can be serviced in an timer tick, schedule from list
            timerSockets = new System.Timers.Timer() { Interval = 1000, Enabled = false }; // Interval >= 750
            timerSockets.Elapsed += (obj, args) =>
            {
                //RunOnUiThread(() =>
                //{
                    if (socket != null) // only if socket exists
                    {
                        // Send a command to the Arduino server on every tick (loop though list)
                        UpdateGUI(executeCommand(commandList[listIndex].Item1), commandList[listIndex].Item2);  //e.g. UpdateGUI(executeCommand("s"), textViewChangePinStateValue);
                        if (++listIndex >= commandList.Count) listIndex = 0;
                    }
                    else timerSockets.Enabled = false;  // If socket broken -> disable timer
                //});
            };

            //Add the "Connect" button handler.
            if (buttonConnect != null)  // if button exists
            {
                buttonConnect.Click += (sender, e) =>
                {
                    //Validate the user input (IP address and port)
                    if (CheckValidIpAddress(editTextIPAddress.Text) && CheckValidPort(editTextIPPort.Text))
                    {
                        
                        if (connector == null) // -> simple sockets
                        {
                            ConnectSocket(editTextIPAddress.Text, editTextIPPort.Text);
                        }
                        else // -> threaded sockets
                        {
                            //Stop the thread If the Connector thread is already started.
                            if (connector.CheckStarted()) connector.StopConnector();
                               connector.StartConnector(editTextIPAddress.Text, editTextIPPort.Text);
                        }
                    }
                    else UpdateConnectionState(3, "Please check IP");
                };
            }

           
            // if (textViewTimerStateValue == TimerSetLight1) { connector = null; }
            //Add the "Change pin state" button handler.
                if (buttonChangePinState != null)
            {
                buttonChangePinState.Click += (sender, e) =>
                {
                    
                    if (connector == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("t"));                 // Send toggle-command to the Arduino
                    }
                    else // -> threaded sockets
                    {
                        if (connector.CheckStarted()) connector.SendMessage("t");  // Send toggle-command to the Arduino
                    }
                };
            }

            if (buttonChangePinState1 != null)
            {
                buttonChangePinState1.Click += (sender, e) =>
                {
                    if (connector1 == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("u"));                 // Send toggle-command to the Arduino
                    }
                    else // -> threaded sockets
                    {
                        if (connector1.CheckStarted()) connector1.SendMessage("u");  // Send toggle-command to the Arduino
                    }
                };
            }

            if (buttonChangePinState2 != null)
            {
                buttonChangePinState2.Click += (sender, e) =>
                {
                    if (connector2 == null) // -> simple sockets
                    {
                        socket.Send(Encoding.ASCII.GetBytes("v"));                 // Send toggle-command to the Arduino
                    }
                    else // -> threaded sockets
                    {
                        if (connector2.CheckStarted()) connector2.SendMessage("v");  // Send toggle-command to the Arduino
                    }
                };
            }


            SensorButton.Click += (object sender, EventArgs e) =>
            {
                
                SensorWaarde = Convert.ToInt16(editTextSensorWaarde.Text);
                if (SensorWaarde ==0)
                {
                    commandList.Add(new Tuple<string, TextView>("z", editTextSensorWaarde));

                }
                if (SensorWaarde == 1)
                {
                    commandList.Add(new Tuple<string, TextView>("d", editTextSensorWaarde));

                }
                if (SensorWaarde == 2)
                {
                    commandList.Add(new Tuple<string, TextView>("e", editTextSensorWaarde));

                }
                if (SensorWaarde == 3)
                {
                    commandList.Add(new Tuple<string, TextView>("f", editTextSensorWaarde));

                }
                if (SensorWaarde == 4)
                {
                    commandList.Add(new Tuple<string, TextView>("g", editTextSensorWaarde));

                }
                if (SensorWaarde == 5)
                {
                    commandList.Add(new Tuple<string, TextView>("h", editTextSensorWaarde));

                }
                if (SensorWaarde == 6)
                {
                    commandList.Add(new Tuple<string, TextView>("j", editTextSensorWaarde));

                }
                if (SensorWaarde == 7)
                {
                    commandList.Add(new Tuple<string, TextView>("k", editTextSensorWaarde));

                }
                if (SensorWaarde == 8)
                {
                    commandList.Add(new Tuple<string, TextView>("l", editTextSensorWaarde));

                }
                if (SensorWaarde == 9)
                {
                    commandList.Add(new Tuple<string, TextView>("m", editTextSensorWaarde));

                }
                if (SensorWaarde == 10)
                {
                    commandList.Add(new Tuple<string, TextView>("n", editTextSensorWaarde));

                }
                if (SensorWaarde == 11)
                {
                    commandList.Add(new Tuple<string, TextView>("o", editTextSensorWaarde));

                }
                if (SensorWaarde == 12)
                {
                    commandList.Add(new Tuple<string, TextView>("p", editTextSensorWaarde));

                }
                if (SensorWaarde == 13)
                {
                    commandList.Add(new Tuple<string, TextView>("q", editTextSensorWaarde));
                }
                    if (SensorWaarde == 14)
                    {
                    commandList.Add(new Tuple<string, TextView>("r", editTextSensorWaarde));

                }
                    if (SensorWaarde == 15)
                    {
                    commandList.Add(new Tuple<string, TextView>("w", editTextSensorWaarde));

                }

                
            };

                CountDownTimer = new System.Timers.Timer();
            

            
            btnTimer.Click += (object sender, EventArgs e) =>
            {
                countTime = Convert.ToInt16(editTextTimerWaarde.Text);




                CountDownTimer.Elapsed += new ElapsedEventHandler(CountDownTimer_Tick);
                CountDownTimer.Interval = 1000;
                CountDownTimer.Start();

                RunOnUiThread(() => { textViewCountDownTimer.Text = countTime.ToString(); });

            };
           
        }
        
        private void CountDownTimer_Tick(object sender, EventArgs e)
        {
            countTime--;
            if (countTime == 0)
            {
                CountDownTimer.Stop();
                socket.Send(Encoding.ASCII.GetBytes("v"));
            }
            RunOnUiThread(() => { textViewCountDownTimer.Text = countTime.ToString(); });
        }



        //Send command to server and wait for response (blocking)
        //Method should only be called when socket existst
        public string executeCommand(string cmd)
        {
            byte[] buffer = new byte[4]; // response is always 4 bytes
            int bytesRead = 0;
            string result = "---";

            if (socket != null)
            {
                //Send command to server
                socket.Send(Encoding.ASCII.GetBytes(cmd));

                try //Get response from server
                {
                    //Store received bytes (always 4 bytes, ends with \n)
                    bytesRead = socket.Receive(buffer);  // If no data is available for reading, the Receive method will block until data is available,
                    //Read available bytes.              // socket.Available gets the amount of data that has been received from the network and is available to be read
                    while (socket.Available > 0) bytesRead = socket.Receive(buffer);
                    if (bytesRead == 4)
                        result = Encoding.ASCII.GetString(buffer, 0, bytesRead - 1); // skip \n
                    else result = "err";
                }
                catch (Exception exception) {
                    result = exception.ToString();
                    if (socket != null) {
                        socket.Close();
                        socket = null;
                    }
                    UpdateConnectionState(3, result);
                }
            }
            return result;


        }

        //Update connection state label (GUI).
        public void UpdateConnectionState(int state, string text)
        {
            // connectButton
            string butConText = "Connect";  // default text
            bool butConEnabled = true;      // default state
            Color color = Color.Red;        // default color
            // pinButton
            bool butPinEnabled = false;     // default state 

            //Set "Connect" button label according to connection state.
            if (state == 1)
            {
                butConText = "Please wait";
                color = Color.Orange;
                butConEnabled = false;
            } else
            if (state == 2)
            {
                butConText = "Disconnect";
                color = Color.Green;
                butPinEnabled = true;
            }
            //Edit the control's properties on the UI thread
            RunOnUiThread(() =>
            {
                textViewServerConnect.Text = text;
                if (butConText != null)  // text existst
                {
                    buttonConnect.Text = butConText;
                    textViewServerConnect.SetTextColor(color);
                    buttonConnect.Enabled = butConEnabled;
                }
                buttonChangePinState.Enabled = butPinEnabled;
                buttonChangePinState1.Enabled = butPinEnabled;
                buttonChangePinState2.Enabled = butPinEnabled;
            });
        }

        //Update GUI based on Arduino response
        public void UpdateGUI(string result, TextView textview)
        {
            RunOnUiThread(() =>
            {
               
                if (result == "OFF") textview.SetTextColor(Color.Red);

                
                else if (result == " ON") textview.SetTextColor(Color.Green);
             
                else textview.SetTextColor(Color.White);  
                textview.Text = result;

                
            });
        }

        // Connect to socket ip/prt (simple sockets)
        public void ConnectSocket(string ip, string prt)
        {
            RunOnUiThread(() =>
            {
                if (socket == null)                                       // create new socket
                {
                    UpdateConnectionState(1, "Connecting...");
                    try  // to connect to the server (Arduino).
                    {
                        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        socket.Connect(new IPEndPoint(IPAddress.Parse(ip), Convert.ToInt32(prt)));
                        if (socket.Connected)
                        {
                            UpdateConnectionState(2, "Connected");
                            timerSockets.Enabled = true;                //Activate timer for communication with Arduino     
                        }
                    } catch (Exception exception) {
                        timerSockets.Enabled = false;
                        if (socket != null)
                        {
                            socket.Close();
                            socket = null;
                        }
                        UpdateConnectionState(4, exception.Message);
                    }
	            }
                else // disconnect socket
                {
                    socket.Close(); socket = null;
                    timerSockets.Enabled = false;
                    UpdateConnectionState(4, "Disconnected");
                }
            });
        }



        //Close the connection (stop the threads) if the application stops.
        protected override void OnStop()
        {
            base.OnStop();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        //Close the connection (stop the threads) if the application is destroyed.
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (connector != null)
            {
                if (connector.CheckStarted())
                {
                    connector.StopConnector();
                }
            }
        }

        

        //Check if the entered IP address is valid.
        private bool CheckValidIpAddress(string ip)
        {
            if (ip != "") {
                //Check user input against regex (check if IP address is not empty).
                Regex regex = new Regex("\\b((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(\\.|$)){4}\\b");
                Match match = regex.Match(ip);
                return match.Success;
            } else return false;
        }

        //Check if the entered port is valid.
        private bool CheckValidPort(string port)
        {
            //Check if a value is entered.
            if (port != "")
            {
                Regex regex = new Regex("[0-9]+");
                Match match = regex.Match(port);

                if (match.Success)
                {
                    int portAsInteger = Int32.Parse(port);
                    //Check if port is in range.
                    return ((portAsInteger >= 0) && (portAsInteger <= 65535));
                }
                else return false;
            } else return false;
        }
        private bool CheckValidWaarde(string waarde)
        {
            if (waarde != "")
            {
                //Check user input against regex (check if IP address is not empty).
                Regex regex = new Regex("\\b((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)(\\.|$)){4}\\b");
                Match match = regex.Match(waarde);
                return match.Success;
            }
            else return false;
        }
    }
}
