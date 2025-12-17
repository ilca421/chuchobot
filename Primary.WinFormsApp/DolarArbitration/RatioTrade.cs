using System;
using System.Diagnostics;

namespace Primary.WinFormsApp
{
    /// <summary>
    /// Permite calcular la ganancia al realizar una operacion de arbitraje de dolar (MEP o CCL)
    /// </summary>
    [DebuggerDisplay("{SellThenBuy.Buy.Instrument.InstrumentId.Symbol} / {BuyThenSell.Buy.Instrument.InstrumentId.Symbol}")]
    public class RatioTrade
    {
        /// <summary>
        /// Instrumento que esta relativamente mas caro
        /// </summary>
        public BuySellTrade SellThenBuy { get; set; }

        /// <summary>
        /// Instrumento que esta relativamente mas barato
        /// </summary>
        public BuySellTrade BuyThenSell { get; set; }

        /// <summary>
        /// Maximo nominal disponible para ejecutar el ciclo completo al precio mostrado (top of book).
        /// </summary>
        public decimal MaxTradableSize => CalculateMaxTradableSize();

        public RatioTrade(BuySellTrade sellThenBuy, BuySellTrade buyThenSell)
        {
            SellThenBuy = sellThenBuy;
            BuyThenSell = buyThenSell;
        }

        public decimal Profit
        {
            get {
                if (BuyThenSell.SellPrice > 0 && SellThenBuy.BuyPrice > 0)
                {
                    return (BuyThenSell.SellPrice / SellThenBuy.BuyPrice) - 1;
                }

                return -100;
            }
        }

        public decimal ProfitLast
        {
            get {
                if (BuyThenSell.Last > 0 && SellThenBuy.Last > 0)
                {
                    return (BuyThenSell.Last / SellThenBuy.Last) - 1;
                }

                return -100;
            }
        }

        /// <summary>
        /// Evalua la disponibilidad de nominales en cada una de las cajas de puntas y devuelve la maxima cantidad actualmente disponible
        /// </summary>
        /// <returns></returns>
        public int GetOwnedVentaMaxSize()
        {
            return (int)MaxTradableSize;
        }

        internal void RefreshData()
        {
            BuyThenSell.RefreshData();
            SellThenBuy.RefreshData();
        }

        private bool HasBookData()
        {
            return SellThenBuy != null && BuyThenSell != null &&
                   SellThenBuy.Sell?.Data?.HasBids() == true &&
                   SellThenBuy.Buy?.Data?.HasOffers() == true &&
                   BuyThenSell.Sell?.Data?.HasOffers() == true &&
                   BuyThenSell.Buy?.Data?.HasBids() == true;
        }

        private decimal CalculateMaxTradableSize()
        {
            if (!HasBookData())
            {
                return 0;
            }

            // 1) Vendo el instrumento caro que tengo (caja de BIDs)
            var ownedSellSize = SellThenBuy.Sell.Data.GetTopBidSize();
            var ownedSellPrice = SellThenBuy.Sell.Data.GetTopBidPrice();
            var ownedSellFactor = SellThenBuy.Sell.Instrument.PriceConvertionFactor;

            // 2) Compro el instrumento barato (caja de OFs)
            var arbBuyOfferSize = BuyThenSell.Sell.Data.GetTopOfferSize();
            var arbBuyOfferPrice = BuyThenSell.Sell.Data.GetTopOfferPrice();
            var arbBuyFactor = BuyThenSell.Sell.Instrument.PriceConvertionFactor;

            // 3) Vendo lo comprado en la pata de arbitraje (caja de BIDs)
            var arbSellBidSize = BuyThenSell.Buy.Data.GetTopBidSize();
            var arbSellBidPrice = BuyThenSell.Buy.Data.GetTopBidPrice();
            var arbSellFactor = BuyThenSell.Buy.Instrument.PriceConvertionFactor;

            // 4) Recompra del instrumento inicial (caja de OFs)
            var ownedBuyOfferSize = SellThenBuy.Buy.Data.GetTopOfferSize();
            var ownedBuyOfferPrice = SellThenBuy.Buy.Data.GetTopOfferPrice();
            var ownedBuyFactor = SellThenBuy.Buy.Instrument.PriceConvertionFactor;

            if (ownedSellPrice <= 0 || arbBuyOfferPrice <= 0 || arbSellBidPrice <= 0 || ownedBuyOfferPrice <= 0)
            {
                return 0;
            }

            // Cash que obtengo vendiendo el "owned" a bid.
            var cashFromOwnedSell = ownedSellSize * ownedSellPrice * ownedSellFactor;
            if (cashFromOwnedSell <= 0)
            {
                return 0;
            }

            // Cuanto puedo comprar en la pata barata con ese cash.
            var arbBuyCapByCash = cashFromOwnedSell / (arbBuyOfferPrice * arbBuyFactor);
            var arbBuyNominal = Math.Min(arbBuyOfferSize, arbBuyCapByCash);
            if (arbBuyNominal <= 0)
            {
                return 0;
            }

            // Cuanto de lo comprado puedo vender en la pata de venta (limitado por bid del book).
            var arbSellNominal = Math.Min(arbBuyNominal, arbSellBidSize);
            if (arbSellNominal <= 0)
            {
                return 0;
            }

            // Cash resultante de la venta del arbitraje.
            var cashFromArbSell = arbSellNominal * arbSellBidPrice * arbSellFactor;
            if (cashFromArbSell <= 0)
            {
                return 0;
            }

            // Con ese cash, cuanto puedo recomprar del instrumento original.
            var ownedBuyCapByCash = cashFromArbSell / (ownedBuyOfferPrice * ownedBuyFactor);
            var ownedBuyNominal = Math.Min(ownedBuyOfferSize, ownedBuyCapByCash);

            // El ciclo completo queda limitado por la pata inicial y por lo que puedo recomprar al final.
            var maxCycleNominal = Math.Min(ownedSellSize, ownedBuyNominal);

            return maxCycleNominal > 0 ? Math.Floor(maxCycleNominal) : 0;
        }
    }
}
