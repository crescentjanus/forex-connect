using System;
using ArgParser;
using System.Threading;
using fxcore2;

namespace CreateEntryOrder
{
    class Program
    {
        static void Main(string[] args)
        {
            O2GSession session = null;
            SessionStatusListener statusListener = null;
            ResponseListener responseListener = null;

            try
            {
                Console.WriteLine("CreateEntryOrder sample\n");

                ArgumentParser argParser = new ArgumentParser(args, "CreateEntryOrder");

                argParser.AddArguments(ParserArgument.Login,
                                       ParserArgument.Password,
                                       ParserArgument.Url,
                                       ParserArgument.Connection,
                                       ParserArgument.SessionID,
                                       ParserArgument.Pin,
                                       ParserArgument.Instrument,
                                       ParserArgument.BuySell,
                                       ParserArgument.Rate,
                                       ParserArgument.Lots,
                                       ParserArgument.AccountID,
                                       ParserArgument.ExpireData);

                argParser.ParseArguments();

                if (!argParser.AreArgumentsValid)
                {
                    argParser.PrintUsage();
                    return;
                }

                argParser.PrintArguments();

                LoginParams loginParams = argParser.LoginParams;
                SampleParams sampleParams = argParser.SampleParams;

                session = O2GTransport.createSession();
                session.useTableManager(O2GTableManagerMode.Yes, null);
                statusListener = new SessionStatusListener(session, loginParams.SessionID, loginParams.Pin);
                session.subscribeSessionStatus(statusListener);
                statusListener.Reset();
                session.login(loginParams.Login, loginParams.Password, loginParams.URL, loginParams.Connection);
                if (statusListener.WaitEvents() && statusListener.Connected)
                {
                    responseListener = new ResponseListener();
                    TableListener tableListener = new TableListener(responseListener);
                    session.subscribeResponse(responseListener);

                    O2GTableManager tableManager = session.getTableManager();
                    O2GTableManagerStatus managerStatus = tableManager.getStatus();
                    while (managerStatus == O2GTableManagerStatus.TablesLoading)
                    {
                        Thread.Sleep(50);
                        managerStatus = tableManager.getStatus();
                    }

                    if (managerStatus == O2GTableManagerStatus.TablesLoadFailed)
                    {
                        throw new Exception("Cannot refresh all tables of table manager");
                    }
                    O2GAccountRow account = GetAccount(tableManager, sampleParams.AccountID);
                    if (account == null)
                    {
                        if (string.IsNullOrEmpty(sampleParams.AccountID))
                        {
                            throw new Exception("No valid accounts");
                        }
                        else
                        {
                            throw new Exception(string.Format("The account '{0}' is not valid", sampleParams.AccountID));
                        }
                    }
                    sampleParams.AccountID = account.AccountID;

                    O2GOfferRow offer = GetOffer(tableManager, sampleParams.Instrument);
                    if (offer == null)
                    {
                        throw new Exception(string.Format("The instrument '{0}' is not valid", sampleParams.Instrument));
                    }

                    O2GLoginRules loginRules = session.getLoginRules();
                    if (loginRules == null)
                    {
                        throw new Exception("Cannot get login rules");
                    }
                    O2GTradingSettingsProvider tradingSettingsProvider = loginRules.getTradingSettingsProvider();
                    int iBaseUnitSize = tradingSettingsProvider.getBaseUnitSize(sampleParams.Instrument, account);
                    int iAmount = iBaseUnitSize * sampleParams.Lots;
                    int iCondDistEntryLimit = tradingSettingsProvider.getCondDistEntryLimit(sampleParams.Instrument);
                    int iCondDistEntryStop = tradingSettingsProvider.getCondDistEntryStop(sampleParams.Instrument);

                    string sOrderType = GetEntryOrderType(offer.Bid, offer.Ask, sampleParams.Rate, sampleParams.BuySell, offer.PointSize, iCondDistEntryLimit, iCondDistEntryStop);

                    tableListener.SubscribeEvents(tableManager);

                    O2GRequest request = CreateEntryOrderRequest(session, offer.OfferID, sampleParams.AccountID, iAmount, sampleParams.Rate, sampleParams.BuySell, sOrderType, sampleParams.ExpireDate);
                    if (request == null)
                    {
                        throw new Exception("Cannot create request");
                    }
                    responseListener.SetRequestID(request.RequestID);
                    tableListener.SetRequestID(request.RequestID);
                    session.sendRequest(request);
                    if (responseListener.WaitEvents())
                    {
                        Console.WriteLine("Done!");
                    }
                    else
                    {
                        throw new Exception("Response waiting timeout expired");
                    }

                    tableListener.UnsubscribeEvents(tableManager);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
            }
            finally
            {
                if (session != null)
                {
                    if (statusListener.Connected)
                    {
                        statusListener.Reset();
                        session.logout();
                        statusListener.WaitEvents();
                        if (responseListener != null)
                            session.unsubscribeResponse(responseListener);
                        session.unsubscribeSessionStatus(statusListener);
                    }
                    session.Dispose();
                }
            }
        }

        /// <summary>
        /// Find valid account
        /// </summary>
        /// <param name="tableManager"></param>
        /// <returns>account</returns>
        private static O2GAccountRow GetAccount(O2GTableManager tableManager, string sAccountID)
        {
            bool bHasAccount = false;
            O2GAccountRow account = null;
            O2GAccountsTable accountsTable = (O2GAccountsTable)tableManager.getTable(O2GTableType.Accounts);
            for (int i = 0; i < accountsTable.Count; i++)
            {
                account = accountsTable.getRow(i);
                string sAccountKind = account.AccountKind;
                if (sAccountKind.Equals("32") || sAccountKind.Equals("36"))
                {
                    if (account.MarginCallFlag.Equals("N"))
                    {
                        if (string.IsNullOrEmpty(sAccountID) || sAccountID.Equals(account.AccountID))
                        {
                            bHasAccount = true;
                            break;
                        }
                    }
                }
            }
            if (!bHasAccount)
            {
                return null;
            }
            else
            {
                return account;
            }
        }

        /// <summary>
        /// Find valid offer by instrument name
        /// </summary>
        /// <param name="tableManager"></param>
        /// <param name="sInstrument"></param>
        /// <returns>offer</returns>
        private static O2GOfferRow GetOffer(O2GTableManager tableManager, string sInstrument)
        {
            bool bHasOffer = false;
            O2GOfferRow offer = null;
            O2GOffersTable offersTable = (O2GOffersTable)tableManager.getTable(O2GTableType.Offers);
            for (int i = 0; i < offersTable.Count; i++)
            {
                offer = offersTable.getRow(i);
                if (offer.Instrument.Equals(sInstrument))
                {
                    if (offer.SubscriptionStatus.Equals("T"))
                    {
                        bHasOffer = true;
                        break;
                    }
                }
            }
            if (!bHasOffer)
            {
                return null;
            }
            else
            {
                return offer;
            }
        }

        /// <summary>
        /// Determine order type based on parameters: current market price of a trading instrument, desired order rate, order direction
        /// </summary>
        /// <param name="dBid"></param>
        /// <param name="dAsk"></param>
        /// <param name="dRate"></param>
        /// <param name="sBuySell"></param>
        /// <param name="dPointSize"></param>
        /// <param name="iCondDistLimit"></param>
        /// <param name="iCondDistStop"></param>
        /// <returns></returns>
        private static string GetEntryOrderType(double dBid, double dAsk, double dRate, string sBuySell, double dPointSize, int iCondDistLimit, int iCondDistStop)
        {
            double dAbsoluteDifference = 0.0D;
            if (sBuySell.Equals(Constants.Buy))
            {
                dAbsoluteDifference = dRate - dAsk;
            }
            else
            {
                dAbsoluteDifference = dBid - dRate;
            }
            int iDifferenceInPips = (int)Math.Round(dAbsoluteDifference / dPointSize);

            if (iDifferenceInPips >= 0)
            {
                if (iDifferenceInPips <= iCondDistStop)
                {
                    throw new Exception("Price is too close to market.");
                }
                return Constants.Orders.StopEntry;
            }
            else
            {
                if (-iDifferenceInPips <= iCondDistLimit)
                {
                    throw new Exception("Price is too close to market.");
                }
                return Constants.Orders.LimitEntry;
            }
        }

        /// <summary>
        /// Create entry order request
        /// </summary>
        private static O2GRequest CreateEntryOrderRequest(O2GSession session, string sOfferID, string sAccountID, int iAmount, double dRate, string sBuySell, string sOrderType, string sExpireDate)
        {
            O2GRequest request = null;
            O2GRequestFactory requestFactory = session.getRequestFactory();
            if (requestFactory == null)
            {
                throw new Exception("Cannot create request factory");
            }
            O2GValueMap valuemap = requestFactory.createValueMap();
            valuemap.setString(O2GRequestParamsEnum.Command, Constants.Commands.CreateOrder);
            valuemap.setString(O2GRequestParamsEnum.OrderType, sOrderType);
            valuemap.setString(O2GRequestParamsEnum.AccountID, sAccountID);
            valuemap.setString(O2GRequestParamsEnum.OfferID, sOfferID);
            valuemap.setString(O2GRequestParamsEnum.BuySell, sBuySell);
            valuemap.setInt(O2GRequestParamsEnum.Amount, iAmount);
            valuemap.setDouble(O2GRequestParamsEnum.Rate, dRate);
            valuemap.setString(O2GRequestParamsEnum.CustomID, "EntryOrder");

            if (!string.IsNullOrEmpty(sExpireDate))
            {
                valuemap.setString(O2GRequestParamsEnum.TimeInForce, Constants.TIF.GTD);
                valuemap.setString(O2GRequestParamsEnum.ExpireDayTime, sExpireDate); // UTCTimestamp format: "yyyyMMdd-HH:mm:ss.SSS" (milliseconds are optional)
            }

            request = requestFactory.createOrderRequest(valuemap);
            if (request == null)
            {
                Console.WriteLine(requestFactory.getLastError());
            }
            return request;
        }
    }
}
