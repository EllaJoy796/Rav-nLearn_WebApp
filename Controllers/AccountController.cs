using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace RavnLearnWeb.Controllers
{
    public class AccountController : Controller
    {
        private bool IsLoggedIn() => HttpContext.Session.GetString("Username") != null;
        private string Username => HttpContext.Session.GetString("Username") ?? "";

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Profile()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");
            ViewBag.Username    = Username;
            ViewBag.Email       = HttpContext.Session.GetString("Email") ?? "";
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

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
                            return RedirectToAction("MyChats", "Chat");
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

        public IActionResult Register()
        {
            return View();
        }

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

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = Database.GetConnection();
                conn.Open();
                using var cmd = new NpgsqlCommand(
                    "UPDATE users SET username=@u, email=@e WHERE username=@old", conn);
                cmd.Parameters.AddWithValue("u",   req.Username);
                cmd.Parameters.AddWithValue("e",   req.Email);
                cmd.Parameters.AddWithValue("old", Username);
                cmd.ExecuteNonQuery();
                HttpContext.Session.SetString("Username", req.Username);
                HttpContext.Session.SetString("Email",    req.Email);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
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

    public class UpdateProfileRequest
    {
        public string Username { get; set; } = "";
        public string Email    { get; set; } = "";
    }
}