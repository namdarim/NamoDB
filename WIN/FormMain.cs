using Microsoft.Extensions.DependencyInjection;
using Namo.App.DBSync;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Namo.WIN
{
    public partial class FormMain : Form
    {
        readonly IServiceProvider _services;
        public FormMain(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            var srv = _services.GetRequiredService<DbSyncAppService>();
            var res = await srv.PullAsync();
            MessageBox.Show(this, res.Message, "Pull Result", MessageBoxButtons.OK, 
                res.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            var srv = _services.GetRequiredService<DbSyncAppService>();
            var res = await srv.PushAsync();
            MessageBox.Show(this, res.Message, "Push Result", MessageBoxButtons.OK,
                res.Succeeded ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }
    }
}
