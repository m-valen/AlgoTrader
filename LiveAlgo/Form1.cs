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


namespace LiveAlgo
{
    public partial class Form1 : Form
    {
        private SterlingLib.STIApp stiApp = new SterlingLib.STIApp();
        private SterlingLib.STIEvents stiEvents = new SterlingLib.STIEvents();
        private SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
        private SterlingLib.STIPosition stiPos = new SterlingLib.STIPosition();
        private SterlingLib.STIQuote stiQuote = new SterlingLib.STIQuote();

        private bool bModeXML = true;

        //[StructLayout(LayoutKind.Sequential)]

        public Form1()
        {
            InitializeComponent();
            stiApp.SetModeXML(true);
            stiEvents.SetOrderEventsAsStructs(true);
            //stiEvents.OnSTIOrderUpdateXML += new SterlingLib._ISTIEventsEvents_OnSTIOrderUpdateXMLEventHandler(OnSTIOrderUpdateXML);
            //stiPos.OnSTIPositionUpdateXML += new SterlingLib._ISTIPositionEvents_OnSTIPositionUpdateXMLEventHandler(OnSTIPositionUpdateXML);

            //Set globals from settings file
            Globals.account = Properties.Settings.Default.SterlingAccount;
            textBox1.Text = Globals.account;

        }

        /*private void OnSTIOrderUpdateXML(ref string strOrder)
        {
            XmlSerializer xs = new XmlSerializer(typeof(SterlingLib.structSTIOrderUpdate));
            SterlingLib.structSTIOrderUpdate structOrder = (SterlingLib.structSTIOrderUpdate)xs.Deserialize(new StringReader(strOrder));
        }
        
        private void OnSTIPositionUpdateXML(ref string strPosition)
        {
            XmlSerializer xs = new XmlSerializer(typeof(SterlingLib.structSTIPositionUpdate));
            SterlingLib.structSTIPositionUpdate structPosition = (SterlingLib.structSTIPositionUpdate)xs.Deserialize(new StringReader(strPosition));
            int netPos = (structPosition.nSharesBot - structPosition.nSharesSld);
            //AddListBoxItem("Postion (XML):  " + structPosition.bstrSym + "  Position =  " + netPos);
        }*/


        private void button1_Click(object sender, EventArgs e)
        {
            SQLiteConnection sqlite_conn;
            SQLiteCommand sqlite_cmd;
            SQLiteDataReader sqlite_datareader;

            //Create DB connection
            sqlite_conn = new SQLiteConnection("Data Source=database.db;Version=3;New=True;Compress=True;");

            //Open connection
            sqlite_conn.Open();

            //Create a SQL command
            sqlite_cmd = sqlite_conn.CreateCommand();



            //Give SQL Query to Command
            sqlite_cmd.CommandText = "CREATE TABLE test (id integer primary key, text varchar(100));";

            //Execute Command
            sqlite_cmd.ExecuteNonQuery();


            //Insert command
            sqlite_cmd.CommandText = "INSERT INTO test (id, text) VALUES (1, 'Text Text 1');";

            sqlite_cmd.ExecuteNonQuery();

            sqlite_cmd.CommandText = "INSERT INTO test (id, text) VALUES (2, 'Test Text 2');";

            sqlite_cmd.ExecuteNonQuery();
            //Read from table
            string stm = "SELECT * FROM test";

            using (SQLiteCommand cmd = new SQLiteCommand(stm,sqlite_conn))
            {
                using (SQLiteDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        Debug.WriteLine(rdr.GetString(1));
                        var a = rdr.GetString(1);
                    }
                }
            }

            //Get data reader object
            //sqlite_datareader = sqlite_cmd.ExecuteReader();


            //Close & clean up connection
            sqlite_conn.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            AlgoForm af = new AlgoForm();
            af.Show();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
            stiOrder.Symbol = "XOP";
            stiOrder.Account = Globals.account;
            stiOrder.Side = "B";
            stiOrder.Quantity = 100;
            stiOrder.Tif = "D"; //day order
            stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
            stiOrder.LmtPrice = Convert.ToDouble(36.00);
            stiOrder.Destination = "BATS";
            stiOrder.ClOrderID = Guid.NewGuid().ToString();
            int order = stiOrder.SubmitOrder();


        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (button4.Text == "Change Sterling Account")
            {
                textBox1.Enabled = true;
                button4.Text = "Set Account";
            }
            else if (button4.Text == "Set Account")
            {
                Properties.Settings.Default.SterlingAccount = textBox1.Text;
                Globals.account = textBox1.Text;
                MessageBox.Show("Account Set");
                button4.Text = "Change Sterling Account";
                textBox1.Enabled = false;
            }
        }
    }
}
