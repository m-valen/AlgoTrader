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

        private SQLiteConnection sqlite_conn;
        private SQLiteCommand sqlite_cmd;
        private SQLiteDataReader sqlite_datareader;

        private string connectionString = "Data Source=database.db;Version=3;New=True;Compress=True;";

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
            using (SQLiteConnection conn = new SQLiteConnection("Data Source=database.db;Version=3;New=True;Compress=True;") )
            {
                conn.Open();

                string stm = "SELECT * FROM Algo WHERE Status='Queued'";

                using (SQLiteCommand cmd = new SQLiteCommand(stm, conn))
                {
                    using (SQLiteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            if (DateTime.ParseExact(rdr.GetString(3), "yyyy-MM-dd HH:mm:ss.ff", null) > DateTime.ParseExact(stiApp.GetServerTime(), "yyyyMMddHHmmss", null)) { 
                                Debug.WriteLine("---------------------");
                                Debug.WriteLine(rdr.GetString(1) + " : " + rdr.GetString(2) + " : " + rdr.GetString(3));

                                AlgoForm af = new AlgoForm(rdr.GetString(1), rdr.GetString(2), DateTime.ParseExact(rdr.GetString(3), "yyyy-MM-dd HH:mm:ss.ff", null), DateTime.ParseExact(rdr.GetString(4), "yyyy-MM-dd HH:mm:ss.ff", null), 
                                    rdr.GetInt32(5), rdr.GetDecimal(6), rdr.GetInt32(7), rdr.GetInt32(10), rdr.GetInt32(11));
                                af.Show();
                            }   
                        }
                    }
                }

                conn.Close();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            AlgoForm af = new AlgoForm();
            af.Show();
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

        private void button5_Click(object sender, EventArgs e)
        {
            string serverTime = stiApp.GetServerTime();
            SterlingLib.structSTIPositionUpdate positionStruct = stiPos.GetPositionInfoStruct("XOP", "E", Globals.account);

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
