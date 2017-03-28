using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
//using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Data.Sql;
using System.IO;
using System.Diagnostics;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;

namespace LiveAlgo
{
    public partial class AlgoForm : Form
    {

        private SterlingLib.STIApp stiApp = new SterlingLib.STIApp();
        private SterlingLib.STIEvents stiEvents = new SterlingLib.STIEvents();
        private SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
        private SterlingLib.STIPosition stiPos = new SterlingLib.STIPosition();
        private SterlingLib.STIQuote stiQuote = new SterlingLib.STIQuote();
        private SterlingLib.ISTIOrderMaint orderMaint = new SterlingLib.STIOrderMaint();

        private SymbolAlgo testAlgo = new SymbolAlgo();

        SQLiteConnection sqlite_conn;
        SQLiteCommand sqlite_cmd;
        SQLiteDataReader sqlite_datareader;


        private string connectionString = "Data Source=database.db;Version=3;New=True;Compress=True;";




        public AlgoForm()
        {
            InitializeComponent();
            stiApp.SetModeXML(true);
            stiEvents.SetOrderEventsAsStructs(true);
            stiEvents.OnSTIOrderUpdateXML += new SterlingLib._ISTIEventsEvents_OnSTIOrderUpdateXMLEventHandler(OnSTIOrderUpdateXML);

            dateTimePicker1.Value = DateTime.Now;

            //Create DB connection
            //sqlite_conn = new SQLiteConnection("Data Source=database.db;Version=3;New=True;Compress=True;");

            //Open connection
            //sqlite_conn.Open();

        }

        private void OnSTIOrderUpdateXML(ref string strOrder)
        {
            if (testAlgo.status == "Stopped")
            {
                return;
            }

            XmlSerializer xs = new XmlSerializer(typeof(SterlingLib.structSTIOrderUpdate));
            SterlingLib.structSTIOrderUpdate structOrder = (SterlingLib.structSTIOrderUpdate)xs.Deserialize(new StringReader(strOrder));

            if (structOrder.bstrSymbol.ToUpper() == testAlgo.symbol.ToUpper() && structOrder.nOrderStatus == 5)
            {   //Limit Order has been filled 
                if (Convert.ToDecimal(structOrder.fLmtPrice) < testAlgo.midPrice)  //Buy order has been filled
                {
                    testAlgo.buyFills++;

                    //Shift orders
                    //Normal behaviour, if lists are full
                    if (testAlgo.ordersAbove.Count == 5 && testAlgo.ordersBelow.Count == 5)
                    {
                        testAlgo.midPrice = Convert.ToDecimal(structOrder.fLmtPrice);

                        //UPDATE METRICS
                        if (testAlgo.currentPosition < 0) // If short, Add to incrementPL
                        {
                            testAlgo.incrementPL += testAlgo.incrementPrice * testAlgo.incrementSize;
                        }

                        testAlgo.currentPosition += structOrder.nQuantity;  //Update position

                        //Move filled order to completed list, remove from active list
                        SterlingLib.ISTIOrder filledOrder = testAlgo.ordersAbove.Find(i => i.ClOrderID == structOrder.bstrClOrderId);
                        testAlgo.ordersBelow.RemoveAt(0);
                        testAlgo.filledOrders.Add(filledOrder);

                        //Insert new order below 5 orders away

                        SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
                        stiOrder.Symbol = testAlgo.symbol;
                        stiOrder.Account = Globals.account;
                        stiOrder.Side = "B";
                        stiOrder.Quantity = testAlgo.incrementSize;
                        stiOrder.Tif = "D"; //day order
                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                        stiOrder.LmtPrice = Convert.ToDouble(testAlgo.midPrice - (testAlgo.incrementPrice * 5));
                        stiOrder.Destination = "BATS";
                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                        int orderStatus = stiOrder.SubmitOrder();
                        if (orderStatus != 0)
                        {
                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                        }
                        else
                        {
                            //Add to appropriate list
                            testAlgo.ordersBelow.Add(stiOrder);
                        }

                        //Cancel the "6th" order from opposite side order list

                        SterlingLib.ISTIOrder cancelOrder = testAlgo.ordersAbove[testAlgo.ordersAbove.Count - 1];
                        orderMaint.CancelOrder(Globals.account, 0, cancelOrder.ClOrderID, Guid.NewGuid().ToString());
                        testAlgo.ordersAbove.RemoveAt(testAlgo.ordersAbove.Count - 1);

                        //Place new order in opposite side order list

                        int orderQuantity = testAlgo.incrementSize;

                        //Autobalance check

                        //If over long

                        if (testAlgo.currentPosition >= testAlgo.autoBalance) orderQuantity = testAlgo.incrementSize * 2;

                        stiOrder = new SterlingLib.STIOrder();
                        stiOrder.Symbol = testAlgo.symbol;
                        stiOrder.Account = Globals.account;
                        stiOrder.Side = "S";
                        stiOrder.Quantity = orderQuantity;
                        stiOrder.Tif = "D"; //day order
                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                        stiOrder.LmtPrice = Convert.ToDouble(testAlgo.midPrice + (testAlgo.incrementPrice));
                        stiOrder.Destination = "BATS";
                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                        orderStatus = stiOrder.SubmitOrder();
                        if (orderStatus != 0)
                        {
                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                        }
                        else
                        {
                            //Add to appropriate list
                            testAlgo.ordersAbove.Insert(0, stiOrder);
                        }

                        //If over autobalance, check orders above and adjust sizes
                        if (testAlgo.currentPosition >= testAlgo.autoBalance) { 

                            int ifFilledPosition = testAlgo.currentPosition - testAlgo.ordersAbove[0].Quantity; 
                            for (int i = 1; i < testAlgo.bracketedOrders; i++)
                            {
                                //Calculation
                                if (ifFilledPosition < testAlgo.autoBalance)
                                {
                                    if (testAlgo.ordersAbove[i].Quantity == testAlgo.incrementSize * 2)  // Order should be reverted to regular increment size
                                    {
                                        //First cancel order
                                        orderMaint.CancelOrder(Globals.account, 0, testAlgo.ordersAbove[i].ClOrderID, Guid.NewGuid().ToString());
                                        //Then resubmit with base increment size
                                        stiOrder = new SterlingLib.STIOrder();
                                        stiOrder.Symbol = testAlgo.symbol;
                                        stiOrder.Account = Globals.account;
                                        stiOrder.Side = "S";
                                        stiOrder.Quantity = testAlgo.incrementSize;
                                        stiOrder.Tif = "D"; //day order
                                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                                        stiOrder.LmtPrice = testAlgo.incrementSize;
                                        stiOrder.Destination = "BATS";
                                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                                        //Remove cancelled order
                                        testAlgo.ordersAbove.RemoveAt(i);

                                        //Submit new order and add to list
                                        orderStatus = stiOrder.SubmitOrder();
                                        if (orderStatus != 0)
                                        {
                                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                                        }
                                        else
                                        {
                                            //Add to appropriate list
                                            testAlgo.ordersAbove.Insert(i, stiOrder);
                                        }
                                        break;  //Should only be one order to adjust
                                    }
                                }
                                ifFilledPosition -= testAlgo.ordersAbove[i].Quantity;
                            }
                        }


                    }
                    foreach (SterlingLib.ISTIOrder order in testAlgo.ordersAbove)
                    {
                        Debug.WriteLine(order.LmtPrice);
                    }
                    foreach (SterlingLib.ISTIOrder order in testAlgo.ordersBelow)
                    {
                        Debug.WriteLine(order.LmtPrice);
                    }
                    Debug.WriteLine("Buy Fills: " + testAlgo.buyFills);
                    Debug.WriteLine("Sell Fills: " + testAlgo.sellFills);
                    Debug.WriteLine("Current Position: " + testAlgo.currentPosition);
                    Debug.WriteLine("Increment PL: " + testAlgo.incrementPL);
                    Debug.WriteLine("--------------");
                    Debug.WriteLine("--------------");

                }
                else if (Convert.ToDecimal(structOrder.fLmtPrice) > testAlgo.midPrice)
                {
                    testAlgo.sellFills++;

                    //Shift orders
                    //Normal behaviour, if lists are full
                    if (testAlgo.ordersAbove.Count == 5 && testAlgo.ordersBelow.Count == 5)
                    {
                        testAlgo.midPrice = Convert.ToDecimal(structOrder.fLmtPrice);

                        //UPDATE METRICS
                        if (testAlgo.currentPosition > 0) // If long, Add to incrementPL
                        {
                            testAlgo.incrementPL += testAlgo.incrementPrice * testAlgo.incrementSize;
                        }
                        testAlgo.currentPosition -= structOrder.nQuantity; //Update position



                        //Move filled order to completed list, remove from active list
                        SterlingLib.ISTIOrder filledOrder = testAlgo.ordersAbove.Find(i => i.ClOrderID == structOrder.bstrClOrderId);
                        testAlgo.ordersAbove.RemoveAt(0);
                        testAlgo.filledOrders.Add(filledOrder);

                        //Insert new order above 5 orders away

                        SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
                        stiOrder.Symbol = testAlgo.symbol;
                        stiOrder.Account = Globals.account;
                        stiOrder.Side = "S";
                        stiOrder.Quantity = testAlgo.incrementSize;
                        stiOrder.Tif = "D"; //day order
                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                        stiOrder.LmtPrice = Convert.ToDouble(testAlgo.midPrice + (testAlgo.incrementPrice * 5));
                        stiOrder.Destination = "BATS";
                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                        int orderStatus = stiOrder.SubmitOrder();
                        if (orderStatus != 0)
                        {
                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                        }
                        else
                        {
                            //Add to appropriate list
                            testAlgo.ordersAbove.Add(stiOrder);
                        }

                        //Cancel the "6th" order from opposite side order list

                        SterlingLib.ISTIOrder cancelOrder = testAlgo.ordersBelow[testAlgo.ordersBelow.Count - 1];
                        orderMaint.CancelOrder(Globals.account, 0, cancelOrder.ClOrderID, Guid.NewGuid().ToString());
                        testAlgo.ordersBelow.RemoveAt(testAlgo.ordersBelow.Count - 1);

                        //Place new order in opposite side order list

                        //Autobalance check

                        int orderQuantity = testAlgo.incrementSize;

                        //If over short

                        if (testAlgo.currentPosition <= -(testAlgo.autoBalance)) orderQuantity = testAlgo.incrementSize * 2;


                        stiOrder = new SterlingLib.STIOrder();
                        stiOrder.Symbol = testAlgo.symbol;
                        stiOrder.Account = Globals.account;
                        stiOrder.Side = "B";
                        stiOrder.Quantity = orderQuantity;
                        stiOrder.Tif = "D"; //day order
                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                        stiOrder.LmtPrice = Convert.ToDouble(testAlgo.midPrice - (testAlgo.incrementPrice));
                        stiOrder.Destination = "BATS";
                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                        orderStatus = stiOrder.SubmitOrder();
                        if (orderStatus != 0)
                        {
                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                        }
                        else
                        {
                            //Add to appropriate list
                            testAlgo.ordersBelow.Insert(0, stiOrder);
                        }

                        //If over autobalance, check orders above and adjust sizes
                        if (testAlgo.currentPosition <= -(testAlgo.autoBalance))
                        {

                            int ifFilledPosition = testAlgo.currentPosition + testAlgo.ordersBelow[0].Quantity;

                            for (int i = 1; i < testAlgo.bracketedOrders; i++)
                            {
                                //Calculation
                                if (ifFilledPosition > -(testAlgo.autoBalance))
                                {
                                    if (testAlgo.ordersBelow[i].Quantity == testAlgo.incrementSize * 2)  // Order should be reverted to regular increment size
                                    {
                                        //First cancel order
                                        orderMaint.CancelOrder(Globals.account, 0, testAlgo.ordersBelow[i].ClOrderID, Guid.NewGuid().ToString());
                                        //Then resubmit with base increment size
                                        stiOrder = new SterlingLib.STIOrder();
                                        stiOrder.Symbol = testAlgo.symbol;
                                        stiOrder.Account = Globals.account;
                                        stiOrder.Side = "B";
                                        stiOrder.Quantity = testAlgo.incrementSize;
                                        stiOrder.Tif = "D"; //day order
                                        stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                                        stiOrder.LmtPrice = testAlgo.ordersBelow[i].LmtPrice;
                                        stiOrder.Destination = "BATS";
                                        stiOrder.ClOrderID = Guid.NewGuid().ToString();

                                        //Remove cancelled order
                                        testAlgo.ordersBelow.RemoveAt(i);

                                        //Submit new order and add to list
                                        orderStatus = stiOrder.SubmitOrder();
                                        if (orderStatus != 0)
                                        {
                                            MessageBox.Show("Order Error: " + orderStatus.ToString());
                                        }
                                        else
                                        {
                                            //Add to appropriate list
                                            testAlgo.ordersBelow.Insert(i, stiOrder);
                                        }
                                        break;  //Should only be one order to adjust
                                    }
                                }
                                ifFilledPosition += testAlgo.ordersBelow[i].Quantity;
                            }
                        }

                    }
                    Debug.WriteLine("Mid Price: " + testAlgo.midPrice);
                    Debug.WriteLine("--------------");
                    foreach (SterlingLib.ISTIOrder order in testAlgo.ordersAbove)
                    {
                        Debug.WriteLine(order.LmtPrice);
                    }
                    foreach (SterlingLib.ISTIOrder order in testAlgo.ordersBelow)
                    {
                        Debug.WriteLine(order.LmtPrice);
                    }
                    Debug.WriteLine("Buy Fills: " + testAlgo.buyFills);
                    Debug.WriteLine("Sell Fills: " + testAlgo.sellFills);
                    Debug.WriteLine("Current Position: " + testAlgo.currentPosition);
                    Debug.WriteLine("Increment PL: " + testAlgo.incrementPL);
                    Debug.WriteLine("--------------");
                    Debug.WriteLine("--------------");

                }
            }



        }

        private void writeAlgoToDB()  //Requires symbol, status, startTime, endTime, bracketedOrders, incrementPrice, incrementSize, Autobalance, HardStopPL
        {
            

            if (testAlgo.symbol != null && testAlgo.status != null && testAlgo.startTime != null && testAlgo.endTime != null && testAlgo.bracketedOrders != null && testAlgo.incrementPrice != null &&
                testAlgo.incrementSize != null && testAlgo.autoBalance != null && testAlgo.hardStop != null) {

                string queryString = "INSERT INTO Algo (Symbol, Status, StartTime, EndTime, BracketedOrders, IncrementPrice, IncrementSize, Autobalance, HardStopPL) VALUES ('" + testAlgo.symbol +
                    "','" + testAlgo.status + "','" + testAlgo.startTime.ToString("yyyy-MM-dd HH:MM:ss.ff") + "','" + testAlgo.endTime.ToString("yyyy-MM-dd HH:MM:ss.ff") + "','" +
                    testAlgo.bracketedOrders + "','" + testAlgo.incrementPrice + "','" + testAlgo.incrementSize + "','" + testAlgo.autoBalance + "','" +
                    testAlgo.hardStop + "');";

                long lastId = 0;

                using (var connection = new SQLiteConnection(
                       connectionString))
                {
                    using (var command = new SQLiteCommand(queryString, connection))
                    {
                        command.Connection.Open();
                        command.ExecuteNonQuery();
                        lastId = connection.LastInsertRowId;
                        testAlgo.DB_ID = Convert.ToInt32(lastId);
                    }
                }

                /*ExecuteNonQuery("INSERT INTO Algo (Symbol, Status, StartTime, EndTime, BracketedOrders, IncrementPrice, IncrementSize, Autobalance, HardStopPL) VALUES ('" + testAlgo.symbol +
                    "','" + testAlgo.status + "','" + testAlgo.startTime.ToString("yyyy-MM-dd HH:MM:ss.ff") + "','" + testAlgo.endTime.ToString("yyyy-MM-dd HH:MM:ss.ff") + "','" +
                    testAlgo.bracketedOrders + "','" + testAlgo.incrementPrice + "','" + testAlgo.incrementSize + "','" + testAlgo.autoBalance + "','" +
                    testAlgo.hardStop + "');");*/

            }
            else
            {
                MessageBox.Show("Can not write to DB - Missing algo values");
            }
        }

        private void updateAlgoStatusDB(string status)
        {
            if (testAlgo.DB_ID != null)
            {

                //sqlite_cmd = sqlite_conn.CreateCommand();
                var query = "UPDATE Algo set Status='" + status + "' WHERE ID='" + testAlgo.DB_ID + "' AND Symbol='" + testAlgo.symbol + "'";

                ExecuteNonQuery(query);

                //sqlite_cmd.CommandText = query;
                //sqlite_cmd.ExecuteNonQuery();
            }
            else
            {
                MessageBox.Show("No ID for symbolAlgo.DB_ID - could not update status in DB. Error from: updateAlgoStatusDB()");
            }
        } 

        private SQLiteDataReader getAlgoFromDB()
        {
            if (testAlgo.DB_ID != null)
            {
                sqlite_cmd = sqlite_conn.CreateCommand();

                string command = "SELECT * FROM Algo WHERE ID='" + testAlgo.DB_ID + "' AND Symbol='" + testAlgo.symbol;
                sqlite_cmd.CommandText = command;

                using (SQLiteDataReader reader = sqlite_cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        return reader;
                    }
                }
            }
            else
            {
                return null;
            }
            return null;
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (!(testAlgo.status == "Running")) { 
                testAlgo.status = "Running";
                label10.Text = "Running";
                label10.Refresh();

                textBox1.Enabled = false;

                decimal startPrice = Convert.ToDecimal(numericUpDown1.Value);  //Also midprice

                testAlgo.symbol = textBox1.Text.ToUpper();
                testAlgo.incrementPrice = numericUpDown8.Value;
                testAlgo.incrementSize = Convert.ToInt32(numericUpDown10.Value);

                testAlgo.Start(startPrice);

                Thread.Sleep(200);

                Debug.WriteLine("Buy Fills: " + testAlgo.buyFills);
                Debug.WriteLine("Sell Fills: " + testAlgo.sellFills);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //Stop

            //Cancel all orders in ordersabove, below, set algo status to stopped

            testAlgo.stopAndCross();

            label10.Text = "Stopped";
            label10.Refresh();

            updateAlgoStatusDB("Stopped");

        }

        private void button3_Click(object sender, EventArgs e)
        {
            //Get Date
            DateTime runDate = dateTimePicker1.Value;
            DateTime startTime = dateTimePicker6.Value;
            DateTime endTime = dateTimePicker7.Value;

            DateTime startDateTime = new DateTime(runDate.Year, runDate.Month, runDate.Day, startTime.Hour, startTime.Minute, startTime.Second);
            DateTime endDateTime = new DateTime(runDate.Year, runDate.Month, runDate.Day, endTime.Hour, endTime.Minute, endTime.Second);

            testAlgo.startTime = startDateTime;
            testAlgo.endTime = endDateTime;

            System.Timers.Timer timeUntilStart = new System.Timers.Timer();
            System.Timers.Timer timeUntilEnd = new System.Timers.Timer();

            testAlgo.bracketedOrders = Convert.ToInt32(numericUpDown4.Value);

            decimal startPrice = Convert.ToDecimal(numericUpDown1.Value);  //Also midprice

            testAlgo.symbol = textBox1.Text.ToUpper();
            testAlgo.incrementPrice = numericUpDown8.Value;
            testAlgo.incrementSize = Convert.ToInt32(numericUpDown10.Value);

            testAlgo.autoBalance = Convert.ToInt32(numericUpDown2.Value);
            testAlgo.hardStop = Convert.ToInt32(numericUpDown3.Value);

            TimeSpan ts = startDateTime - DateTime.Now;
            TimeSpan ets = endDateTime - DateTime.Now;

            if (ts > new TimeSpan(0)) timeUntilStart.Interval = ts.TotalMilliseconds;
            else { MessageBox.Show("Start Time must be later than current time."); return; }

            if (ets > new TimeSpan(0)) timeUntilEnd.Interval = ets.TotalMilliseconds;
            else { MessageBox.Show("End Time must be later than current time."); return; }
            textBox1.Enabled = false;

            ElapsedEventHandler handler = new ElapsedEventHandler(delegate (object o, ElapsedEventArgs f)
            {
                if (testAlgo.status == "Queued")
                {
                    testAlgo.status = "Running";
                    if (this.label10.InvokeRequired) {
                        this.label10.BeginInvoke((MethodInvoker)delegate () { this.label10.Text = "Running"; this.label10.Refresh(); });
                    }
                    else
                    {
                        label10.Text = "Running";
                        label10.Refresh();
                    }
                    

                    

                    testAlgo.Start(startPrice);

                    updateAlgoStatusDB("Running");

                    Thread.Sleep(200);
                }
            });
            timeUntilStart.Elapsed += handler;
            timeUntilStart.Start();

            ElapsedEventHandler endHandler = new ElapsedEventHandler(delegate (object o, ElapsedEventArgs f)
            {
                if (testAlgo.status == "Running")
                {
                    testAlgo.status = "Stopped";
                    if (this.label10.InvokeRequired)
                    {
                        this.label10.BeginInvoke((MethodInvoker)delegate () { this.label10.Text = "Stopped"; this.label10.Refresh(); });
                    }
                    else
                    {
                        label10.Text = "Stopped";
                        label10.Refresh();
                    }




                    testAlgo.stopAndCross();

                    updateAlgoStatusDB("Stopped");

                    Thread.Sleep(200);
                }
            });
            timeUntilEnd.Elapsed += endHandler;
            timeUntilEnd.Start();

            testAlgo.status = "Queued";

            writeAlgoToDB();
           
            label10.Text = "Queued";
            label10.Refresh();

            



        }
        private void ExecuteNonQuery(string queryString)
        {
            using (var connection = new SQLiteConnection(
                       connectionString))
            {
                using (var command = new SQLiteCommand(queryString, connection))
                {
                    command.Connection.Open();
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
