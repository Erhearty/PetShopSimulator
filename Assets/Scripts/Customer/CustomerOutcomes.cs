namespace PetShop.Customer
{
    /// <summary>Why a shopper left with an empty basket, as raised by <see cref="CustomerAI"/> for telemetry.</summary>
    public enum WalkoutReason
    {
        /// <summary>No walkout recorded.</summary>
        None,
        /// <summary>No shelf (or pen) had anything they were after.</summary>
        NoStockForWant,
        /// <summary>They found something they wanted but it cost more than they would pay.</summary>
        TooExpensive,
        /// <summary>At least one walk to a shelf or pen failed (timed out or had no path).</summary>
        CouldNotReachShelf,
        /// <summary>Stock was there and affordable, but every purchase roll failed.</summary>
        NotTempted,
    }

    /// <summary>Which leg of a shopper's visit a walk belongs to, so a navigation timeout names its target.</summary>
    public enum NavLeg
    {
        /// <summary>No walk yet.</summary>
        None,
        /// <summary>Pavement to the forecourt on the way in.</summary>
        ForecourtIn,
        /// <summary>To a shelf or pen while browsing.</summary>
        Browse,
        /// <summary>To the register when the shop has no queue.</summary>
        Register,
        /// <summary>To a place in the checkout queue.</summary>
        Queue,
        /// <summary>Off the queue line after being served.</summary>
        StepAside,
        /// <summary>Back out to the forecourt on the way out.</summary>
        ForecourtOut,
        /// <summary>From the forecourt along the pavement to despawn.</summary>
        Exit,
    }
}
