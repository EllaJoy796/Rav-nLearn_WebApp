using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Text;
using System.Linq;

namespace RavnLearnWeb.Controllers
{
    public class ChatController : Controller
    {
        private bool IsLoggedIn() => HttpContext.Session.GetString("Username") != null;
        private int UserId => HttpContext.Session.GetInt32("UserId") ?? 0;
        private string Username => HttpContext.Session.GetString("Username") ?? "";

        public IActionResult Index()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

        public IActionResult MyChats()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

            public IActionResult Flashcards()
            {
                if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
                ViewBag.Username = Username;
                ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
                return View();
            }

            public IActionResult Quizzes()
            {
                if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
                ViewBag.Username = Username;
                ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
                return View();
            }
        [HttpGet]
        public async Task<IActionResult> GetChats()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "SELECT chat_id, title FROM chats WHERE user_id = @uid ORDER BY created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    chats.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpPost]
        public async Task<IActionResult> NewChat()
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO chats (user_id, title, created_at) VALUES (@uid, @title, @now) RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("title", "New Chat");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteChat(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var cmd0 = new NpgsqlCommand("DELETE FROM chat_documents WHERE chat_id = @cid", conn);
                cmd0.Parameters.AddWithValue("cid", id);
                await cmd0.ExecuteNonQueryAsync();

                using var cmd1 = new NpgsqlCommand("DELETE FROM messages WHERE chat_id = @cid", conn);
                cmd1.Parameters.AddWithValue("cid", id);
                await cmd1.ExecuteNonQueryAsync();

                using var cmd2 = new NpgsqlCommand(
                    "DELETE FROM chats WHERE chat_id = @cid AND user_id = @uid", conn);
                cmd2.Parameters.AddWithValue("cid", id);
                cmd2.Parameters.AddWithValue("uid", UserId);
                await cmd2.ExecuteNonQueryAsync();

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var messages = new List<object>();
            var docFilenames = new List<string>();
            string? chatTitle = null;

            try
            {
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT title FROM chats WHERE chat_id = @cid", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    var result = await cmd.ExecuteScalarAsync();
                    chatTitle = result as string;
                }

                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT document_filename FROM chat_documents WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        docFilenames.Add(reader.GetString(0));
                }

                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT role, content FROM messages WHERE chat_id = @cid ORDER BY timestamp ASC", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        messages.Add(new { role = reader.GetString(0), content = reader.GetString(1) });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }

            return Json(new
            {
                messages,
                docFilenames,
                docFilename = docFilenames.FirstOrDefault(),
                chatTitle
            });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Message))
                return BadRequest(new { error = "Empty message" });

            try
            {
                int chatId = req.ChatId;

                if (chatId <= 0)
                {
                    using var conn = RavnLearnWeb.Database.GetConnection();
                    await conn.OpenAsync();
                    using var newCmd = new NpgsqlCommand(
                        "INSERT INTO chats (user_id, title, created_at) VALUES (@uid, @title, @now) RETURNING chat_id", conn);
                    newCmd.Parameters.AddWithValue("uid", UserId);
                    newCmd.Parameters.AddWithValue("title", "New Chat");
                    newCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                    chatId = Convert.ToInt32(await newCmd.ExecuteScalarAsync());
                }

                string newTitle = "";
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var countCmd = new NpgsqlCommand(
                        "SELECT COUNT(*) FROM messages WHERE chat_id = @cid AND role = 'user'", conn);
                    countCmd.Parameters.AddWithValue("cid", chatId);
                    long count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

                    if (count == 0)
                    {
                        newTitle = req.Message.Length > 40
                            ? req.Message.Substring(0, 40) + "…"
                            : req.Message;
                        using var titleCmd = new NpgsqlCommand(
                            "UPDATE chats SET title = @t WHERE chat_id = @cid", conn);
                        titleCmd.Parameters.AddWithValue("t", newTitle);
                        titleCmd.Parameters.AddWithValue("cid", chatId);
                        await titleCmd.ExecuteNonQueryAsync();
                    }
                }

                var docContextBuilder = new StringBuilder();
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var dcmd = new NpgsqlCommand(
                        "SELECT document_filename, document_text FROM chat_documents WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
                    dcmd.Parameters.AddWithValue("cid", chatId);
                    using var dr = await dcmd.ExecuteReaderAsync();
                    while (await dr.ReadAsync())
                    {
                        docContextBuilder.AppendLine($"=== Document: {dr.GetString(0)} ===");
                        docContextBuilder.AppendLine(dr.GetString(1));
                        docContextBuilder.AppendLine();
                    }
                }

                await SaveMessage(chatId, "user", req.Message);
                string reply = await GeminiService.AskAsync(req.Message, docContextBuilder.ToString());
                await SaveMessage(chatId, "ai", reply);

                return Json(new { reply, chatId, newTitle });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument(IFormFile file, int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            string ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".pdf" && ext != ".docx" && ext != ".txt")
                return BadRequest(new { error = "Only PDF, DOCX, and TXT files are supported." });

            try
            {
                if (chatId <= 0)
                {
                    using var conn0 = RavnLearnWeb.Database.GetConnection();
                    await conn0.OpenAsync();
                    using var nc = new NpgsqlCommand(
                        "INSERT INTO chats (user_id, title, created_at) VALUES (@uid, @title, @now) RETURNING chat_id", conn0);
                    nc.Parameters.AddWithValue("uid", UserId);
                    nc.Parameters.AddWithValue("title", "New Chat");
                    nc.Parameters.AddWithValue("now", DateTime.UtcNow);
                    chatId = Convert.ToInt32(await nc.ExecuteScalarAsync());
                }

                string fileName = file.FileName;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                byte[] fileBytes = ms.ToArray();

                string docText = ext == ".txt" ? Encoding.UTF8.GetString(fileBytes)
                               : ext == ".pdf" ? ExtractPdfText(fileBytes)
                               : ExtractDocxText(fileBytes);

                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chat_documents (chat_id, document_filename, document_text, uploaded_at)
                      VALUES (@cid, @fn, @dt, @now)", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                cmd.Parameters.AddWithValue("fn", fileName);
                cmd.Parameters.AddWithValue("dt", docText);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();

                string welcome = $"Document \"{fileName}\" loaded! You can ask me questions about it, request a summary, or say \"create a quiz\".";
                await SaveMessage(chatId, "ai", welcome);

                return Json(new { success = true, chatId, fileName, welcome });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> SaveQuiz([FromBody] SaveQuizRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "INSERT INTO quizzes (chat_id, quiz_text, created_at) VALUES (@cid, @qt, @now) RETURNING quiz_id", conn);
                cmd.Parameters.AddWithValue("cid", req.ChatId);
                cmd.Parameters.AddWithValue("qt", req.QuizText);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { success = true, quizId = newId });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizzes(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var quizzes = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    "SELECT quiz_id, quiz_text FROM quizzes WHERE chat_id = @cid ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    quizzes.Add(new { id = reader.GetInt32(0), text = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(quizzes);
        }

        private async Task SaveMessage(int chatId, string role, string content)
        {
            using var conn = RavnLearnWeb.Database.GetConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                "INSERT INTO messages (chat_id, role, content, timestamp) VALUES (@cid, @role, @content, @ts)", conn);
            cmd.Parameters.AddWithValue("cid", chatId);
            cmd.Parameters.AddWithValue("role", role);
            cmd.Parameters.AddWithValue("content", content);
            cmd.Parameters.AddWithValue("ts", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        private string ExtractPdfText(byte[] bytes)
        {
            var sb = new StringBuilder();
            try
            {
                using var doc = UglyToad.PdfPig.PdfDocument.Open(bytes);
                foreach (var page in doc.GetPages())
                {
                    var words = page.GetWords();
                    sb.AppendLine(string.Join(" ", words.Select(w => w.Text)));
                }

                string result = sb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(result))
                    return "[This PDF appears to be image-based or scanned. Text could not be extracted.]";

                return result;
            }
            catch (Exception ex)
            {
                return $"[Could not extract PDF text: {ex.Message}]";
            }
        }

        private string ExtractDocxText(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(ms, false);
                return doc.MainDocumentPart?.Document.Body?.InnerText ?? "";
            }
            catch { return "[Could not extract DOCX text]"; }
        }
    }

    public class SendMessageRequest
    {
        public int ChatId { get; set; }
        public string Message { get; set; } = "";
    }

    public class SaveQuizRequest
    {
        public int ChatId { get; set; }
        public string QuizText { get; set; } = "";
    }
}