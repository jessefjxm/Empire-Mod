namespace FactionColonies {
    public class FCWindow_Pay_Silver_Loyalty : FCWindow_Pay_Silver {
        public FCWindow_Pay_Silver_Loyalty(SettlementFC settlement) : base(settlement) {
            this.forcePause = false;
            this.draggable = true;
            this.doCloseX = true;
            this.preventCameraMotion = false;
            this.silverCount = PaymentUtil.getSilver();
            this.settlement = settlement;
            this.selectedSilver = 0;
            this.stringEffect = "SettlementGainsXLoyalty";
        }


        public override float returnValue(int silver) {
            return silver / 100f;
        }

        public override void useValue(float value) {
            settlement.loyalty += returnValue(selectedSilver);
            if (settlement.loyalty > 100)
                settlement.loyalty = 100;
        }



    }
}

