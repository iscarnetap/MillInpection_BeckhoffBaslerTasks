using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BeckhoffBasler
{
    public partial class frmRunning : Form
    {
        public frmRunning()
        {
            InitializeComponent();
        }

        public bool bSwowProgram = false;
        public int frames = 0;
        public void btnShowMain_Click(object sender, EventArgs e)
        {
            bSwowProgram = true;
        }

        private void lblSnap_TextChanged(object sender, EventArgs e)
        {
            try
            {
                int res = 0;
                if (int.TryParse(lblSnap.Text, out res))
                {
                    progressBar1.Maximum = frames;
                    progressBar1.Value = res;
                    this.Opacity =1.2 - (20 + 80 * ((Single)res / (Single)frames))/100.0;
                }
            }
            catch (Exception ex) { }
        }
    }
}
