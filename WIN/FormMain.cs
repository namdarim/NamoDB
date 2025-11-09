using Microsoft.Extensions.DependencyInjection;
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

        private void button1_Click(object sender, EventArgs e)
        {
            var srv = _services.GetRequiredService<Infrastructure.DBSync.SyncOrchestrator>();
            srv.PullAsync(@"C:\temp\namo-local.db").GetAwaiter().GetResult();
        }
    }
}
