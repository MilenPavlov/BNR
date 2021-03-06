
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace RaiseMan
{
    public partial class Preference : AppKit.NSPanel
    {
        #region Constructors

        // Called when created from unmanaged code
        public Preference(IntPtr handle) : base(handle)
        {
            Initialize();
        }
        
        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public Preference(NSCoder coder) : base(coder)
        {
            Initialize();
        }
        
        // Shared initialization code
        void Initialize()
        {
        }

        #endregion
    }
}

