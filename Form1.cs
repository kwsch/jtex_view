using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CTR;

namespace jtex
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (Directory.Exists(files[0])) // allow folder
                files = Directory.GetFiles(files[0], "*.*", SearchOption.TopDirectoryOnly);
            foreach (string f in files)
                openFile(f);
        }
        private void openFile(string path)
        {
            // Handle file
            try
            {
                if (!File.Exists(path)) throw new Exception("Can only accept files, not folders.");
                pictureBox1.Image = Picross.makeBMP(path, checkBox1.Checked);
            }
            catch (Exception e) { System.Media.SystemSounds.Asterisk.Play(); }
        }
    }
}
