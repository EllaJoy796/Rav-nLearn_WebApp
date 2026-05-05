using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace RavnLearnWeb.Controllers
{
    public class AccountController : Controller
    {
        // GET: /Account/Login
        public IActionResult Login()
        {
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            try
            {
                using (var conn = Database.GetConnection())
                {
                    conn.Open();
                    string sql = "SELECT user_id FROM users WHERE username=@u AND password_hash=@p";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        cmd.Parameters.AddWithValue("p", HashPassword(password));

                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            HttpContext.Session.SetString("Username", username);
                            HttpContext.Session.SetInt32("UserId", Convert.ToInt32(result));
                            return RedirectToAction("Index", "Chat");
                        }
                        else
                        {
                            ViewBag.Error = "Invalid username or password.";
                            return View();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Connection error: " + ex.Message;
                return View();
            }
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public IActionResult Register(string username, string email, string password, string confirmPassword)
        {
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match.";
                return View();
            }

            if (string.IsNullOrEmpty(email) || !email.Contains("@"))
            {
                ViewBag.Error = "Please enter a valid email.";
                return View();
            }

            try
            {
                using (var conn = Database.GetConnection())
                {
                    conn.Open();
                    string sql = "INSERT INTO users (username, email, password_hash, created_at) VALUES (@u, @e, @p, @d)";
                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("u", username);
                        cmd.Parameters.AddWithValue("e", email);
                        cmd.Parameters.AddWithValue("p", HashPassword(password));
                        cmd.Parameters.AddWithValue("d", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        return RedirectToAction("Login");
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error: " + ex.Message;
                return View();
            }
        }

        // GET: /Account/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        private string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            }
        }
    }
}