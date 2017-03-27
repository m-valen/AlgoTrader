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
    class SymbolAlgo
    {
        //Parameters
        public string strategy = "Toggle";
        public string status;   //Waiting (for start), Running, Completed
        public string symbol;
        public DateTime startTime;
        public DateTime endTime;
        public decimal incrementPrice;
        public int incrementSize;
        public int autoBalance;
        public decimal hardStop;
        public int bracketedOrders;

        //Status
        public int buyFills;
        public int sellFills;
        public decimal startPrice; //OnStart
        public decimal finalPrice; //OnComplete
        public decimal incrementPL;
        public decimal priceMovePL;
        public decimal totalPL;

        public decimal maxUnrealized;
        public decimal minUnrealized;

        public decimal realizedPL;
        public decimal unrealizedPL;

        public DateTime stopTime;

        public int currentPosition;

        //Orders info, live management
        public List<SterlingLib.ISTIOrder> ordersAbove = new List<SterlingLib.ISTIOrder>();

        public List<SterlingLib.ISTIOrder> ordersBelow = new List<SterlingLib.ISTIOrder>();

        public List<SterlingLib.ISTIOrder> filledOrders = new List<SterlingLib.ISTIOrder>();

        public decimal midPrice;     //OnChange, update orders


        

        

        public SymbolAlgo()
        {
            
        }

        

        public void SetMidPrice(int price)
        {
            //Change MidPrice, and update sitting orders

        }

        public void Start(decimal _midPrice)
        {
            midPrice = _midPrice;

            ordersBelow.Clear();
            ordersAbove.Clear();
            //Create orders around midprice...

            //Closest first...               


            List<decimal> orderPrices = new List<decimal>();
            orderPrices.Add(midPrice + incrementPrice);
            orderPrices.Add(midPrice - incrementPrice);
            orderPrices.Add(midPrice + (incrementPrice * 2));
            orderPrices.Add(midPrice - (incrementPrice * 2));
            orderPrices.Add(midPrice + (incrementPrice * 3));
            orderPrices.Add(midPrice - (incrementPrice * 3));
            orderPrices.Add(midPrice + (incrementPrice * 4));
            orderPrices.Add(midPrice - (incrementPrice * 4));
            orderPrices.Add(midPrice + (incrementPrice * 5));
            orderPrices.Add(midPrice - (incrementPrice * 5));

            foreach(decimal price in orderPrices)
            {
                //Create order, execute, add to list
                string side = null;
                if (price < midPrice) side = "B";
                if (price > midPrice) side = "S";

                SterlingLib.STIOrder stiOrder = new SterlingLib.STIOrder();
                stiOrder.Symbol = symbol;
                stiOrder.Account = Globals.account;
                if (side != null) stiOrder.Side = side;
                stiOrder.Quantity = 100;
                stiOrder.Tif = "D"; //day order
                stiOrder.PriceType = SterlingLib.STIPriceTypes.ptSTILmt;
                stiOrder.LmtPrice = Convert.ToDouble(price);
                stiOrder.Destination = "BATS";
                stiOrder.ClOrderID = Guid.NewGuid().ToString();

                if (side == "B") ordersBelow.Add(stiOrder);
                else if (side == "S") ordersAbove.Add(stiOrder);
            }

            if (ordersBelow.Count == 5 && ordersAbove.Count == 5)
            {
                

                for (int i = 0; i <= 4; i++)
                {
                    int orderStatus = ordersAbove[i].SubmitOrder();
                    if (orderStatus != 0)
                    {
                        MessageBox.Show("Order Error: " + orderStatus.ToString());
                    }
                    orderStatus = ordersBelow[i].SubmitOrder();
                    if (orderStatus != 0)
                    {
                        MessageBox.Show("Order Error: " + orderStatus.ToString());
                    }
                
                }
            
            }
        }

    }
}
