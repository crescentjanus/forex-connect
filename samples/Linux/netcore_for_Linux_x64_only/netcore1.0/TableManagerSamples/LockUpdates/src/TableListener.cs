using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using fxcore2;

namespace LockUpdates
{
    /// <summary>
    /// Orders table controller to listen updates and create orders
    /// </summary>
    class TableListener : IO2GTableListener
    {
        private ResponseListener mResponseListener;
        private string mRequestID;

        // ctor
        public TableListener(ResponseListener responseListener)
        {
            mResponseListener = responseListener;
            mRequestID = string.Empty;
        }

        public void SetRequestID(string sRequestID)
        {
            mRequestID = sRequestID;
        }

        // Implementation of IO2GTableListener interface public method onAdded
        public void onAdded(string sRowID, O2GRow rowData)
        {
            if (rowData.TableType == O2GTableType.Orders) {
                O2GOrderRow orderRow = (O2GOrderRow)rowData;
                if (mRequestID.Equals(orderRow.RequestID)) {
                    Console.WriteLine("The order has been added. OrderID={0}, Type={1}, BuySell={2}, Rate={3}, TimeInForce={4}",
                            orderRow.OrderID, orderRow.Type, orderRow.BuySell, orderRow.Rate, orderRow.TimeInForce);
                    mResponseListener.StopWaiting();
                }
            }
        }

        // Implementation of IO2GTableListener interface public method onChanged
        public void onChanged(string sRowID, O2GRow rowData)
        {
        }

        // Implementation of IO2GTableListener interface public method onDeleted
        public void onDeleted(string sRowID, O2GRow rowData)
        {
        }

        public void onStatusChanged(O2GTableStatus status)
        {
        }

        public void SubscribeEvents(O2GTableManager manager)
        {
            O2GOrdersTable ordersTable = (O2GOrdersTable)manager.getTable(O2GTableType.Orders);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Insert, this);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Update, this);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Delete, this);
        }

        public void UnsubscribeEvents(O2GTableManager manager)
        {
            O2GOrdersTable ordersTable = (O2GOrdersTable)manager.getTable(O2GTableType.Orders);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Insert, this);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Update, this);
            ordersTable.subscribeUpdate(O2GTableUpdateType.Delete, this);
        }
    }
}
