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
    public partial class AlgoForm : Form
    {
        
        private SymbolAlgo testAlgo = new SymbolAlgo();

        public AlgoForm()
        {
            InitializeComponent();
            
        }
        

        private void button1_Click(object sender, EventArgs e)
        {
            decimal startPrice = Convert.ToDecimal(36.00);  //Also midprice

            testAlgo.symbol = "XOP";
            testAlgo.incrementPrice = Convert.ToDecimal(0.10);
            testAlgo.incrementSize = 100;

            testAlgo.Start(startPrice);

            Debug.WriteLine("Buy Fills: " + testAlgo.buyFills);
            Debug.WriteLine("Sell Fills: " + testAlgo.sellFills);
        }
    }
}
