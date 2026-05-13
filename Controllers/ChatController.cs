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
            ViewBag.Flashcards = null;
            return View();
        }

        public IActionResult FlashcardView(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.SetName   = "FLASHCARD SET";
            ViewBag.SetDate   = DateTime.Now.ToString("MMM dd, yyyy");
            ViewBag.CardCount = 0;
            ViewBag.Cards     = null;
            ViewBag.ChatId    = id;
            ViewBag.Username  = Username;
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
                    @"SELECT chat_id, COALESCE(name, 'New Chat') AS title 
                    FROM chats WHERE user_id = @uid 
                    ORDER BY created_at DESC", conn);
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
                    "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", "New Chat");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, name, subject, created_at)
                    VALUES (@uid, @name, @subject, @now)
                    RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("subject", req.Subject ?? "");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, name = req.Name, subject = req.Subject ?? "" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyChats()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT c.chat_id,
                            COALESCE(c.name, 'New Chat') AS name,
                            COALESCE(c.subject, '')      AS subject,
                            c.created_at,
                            COUNT(f.file_id)             AS file_count
                    FROM chats c
                    LEFT JOIN files f ON f.chat_id = c.chat_id
                    WHERE c.user_id = @uid
                    GROUP BY c.chat_id
                    ORDER BY c.created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    chats.Add(new
                    {
                        id        = reader.GetInt32(0),
                        name      = reader.GetString(1),
                        subject   = reader.GetString(2),
                        date      = reader.GetDateTime(3).ToString("MMM dd, yyyy"),
                        fileCount = reader.GetInt64(4)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteChat(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var cmdF = new NpgsqlCommand("DELETE FROM files WHERE chat_id = @cid", conn);
                cmdF.Parameters.AddWithValue("cid", id);
                await cmdF.ExecuteNonQueryAsync();

                using var cmdQ = new NpgsqlCommand("DELETE FROM quizzes WHERE chat_id = @cid", conn);
                cmdQ.Parameters.AddWithValue("cid", id);
                await cmdQ.ExecuteNonQueryAsync();

                using var cmdFc = new NpgsqlCommand("DELETE FROM flashcards WHERE chat_id = @cid", conn);
                cmdFc.Parameters.AddWithValue("cid", id);
                await cmdFc.ExecuteNonQueryAsync();

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
            string chatTitle = "New Chat";

            try
            {
                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                       "SELECT COALESCE(name, 'New Chat') FROM chats WHERE chat_id = @cid", conn);
                    cmd.Parameters.AddWithValue("cid", chatId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                        chatTitle = result.ToString() ?? "New Chat";
                }

                using (var conn = RavnLearnWeb.Database.GetConnection())
                {
                    await conn.OpenAsync();
                    using var cmd = new NpgsqlCommand(
                        "SELECT filename FROM files WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
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
                        "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn);
                    newCmd.Parameters.AddWithValue("uid", UserId);
                    newCmd.Parameters.AddWithValue("name", "New Chat");
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
                            "UPDATE chats SET name = @t WHERE chat_id = @cid", conn);
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
                         "SELECT filename, extracted_text FROM files WHERE chat_id = @cid ORDER BY uploaded_at ASC", conn);
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
                        "INSERT INTO chats (user_id, name, created_at) VALUES (@uid, @name, @now) RETURNING chat_id", conn0);
                    nc.Parameters.AddWithValue("uid", UserId);
                    nc.Parameters.AddWithValue("name", "New Chat");
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
                    @"INSERT INTO files (chat_id, filename, extracted_text, uploaded_at)
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

        [HttpPost]
        public async Task<IActionResult> SaveQuizQuestions([FromBody] SaveQuizQuestionsRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                foreach (var q in req.Questions)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO quizzes (chat_id, question, choice_a, choice_b, choice_c, choice_d, correct_answer, created_at)
                        VALUES (@cid, @q, @a, @b, @c, @d, @ans, @now)", conn);
                    cmd.Parameters.AddWithValue("cid", req.ChatId);
                    cmd.Parameters.AddWithValue("q",   q.Question);
                    cmd.Parameters.AddWithValue("a",   q.ChoiceA);
                    cmd.Parameters.AddWithValue("b",   q.ChoiceB);
                    cmd.Parameters.AddWithValue("c",   q.ChoiceC);
                    cmd.Parameters.AddWithValue("d",   q.ChoiceD);
                    cmd.Parameters.AddWithValue("ans", q.CorrectAnswer);
                    cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetQuizzesByChat(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var questions = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT quiz_id, question, choice_a, choice_b, choice_c, choice_d, correct_answer
                    FROM quizzes WHERE chat_id = @cid ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var question = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    if (string.IsNullOrEmpty(question)) continue; // skip old quiz_text-only rows
                    questions.Add(new {
                        id            = reader.GetInt32(0),
                        question,
                        choiceA       = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        choiceB       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        choiceC       = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        choiceD       = reader.IsDBNull(5) ? "" : reader.GetString(5),
                        correctAnswer = reader.IsDBNull(6) ? "" : reader.GetString(6)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(questions);
        }

        [HttpPost]
        public async Task<IActionResult> SaveFlashcards([FromBody] SaveFlashcardsRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                foreach (var card in req.Cards)
                {
                    using var cmd = new NpgsqlCommand(
                        @"INSERT INTO flashcards (chat_id, front, back, created_at)
                        VALUES (@cid, @front, @back, @now)", conn);
                    cmd.Parameters.AddWithValue("cid",   req.ChatId);
                    cmd.Parameters.AddWithValue("front", card.Front);
                    cmd.Parameters.AddWithValue("back",  card.Back);
                    cmd.Parameters.AddWithValue("now",   DateTime.UtcNow);
                    await cmd.ExecuteNonQueryAsync();
                }
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetFlashcardsByChat(int chatId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var cards = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT flashcard_id, front, back FROM flashcards
                    WHERE chat_id = @cid ORDER BY created_at ASC", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    cards.Add(new {
                        id    = reader.GetInt32(0),
                        front = reader.GetString(1),
                        back  = reader.GetString(2)
                    });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(cards);
        }

        [HttpPost]
        public async Task<IActionResult> NewSession([FromBody] NewSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var check = new NpgsqlCommand(
                    "SELECT 1 FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                check.Parameters.AddWithValue("pid", req.FolderId);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null) return Forbid();

                using var chatCmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, project_id, name, created_at)
                    VALUES (@uid, @pid, 'New Chat', @now)
                    RETURNING chat_id", conn);
                chatCmd.Parameters.AddWithValue("uid", UserId);
                chatCmd.Parameters.AddWithValue("pid", req.FolderId);
                chatCmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int chatId = Convert.ToInt32(await chatCmd.ExecuteScalarAsync());

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chat_sessions (chat_id, title, created_at)
                    VALUES (@cid, 'New Chat', @now)
                    RETURNING session_id", conn);
                cmd.Parameters.AddWithValue("cid", chatId);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions(int folderId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var sessions = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT s.session_id, s.title
                    FROM chat_sessions s
                    JOIN chats c ON c.chat_id = s.chat_id
                    JOIN projects p ON p.project_id = c.project_id
                    WHERE p.project_id = @pid AND p.user_id = @uid
                    ORDER BY s.created_at DESC", conn);
                cmd.Parameters.AddWithValue("pid", folderId);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    sessions.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(sessions);
        }

        public IActionResult Quizzes()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.Username = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            ViewBag.Quizzes = null;
            return View();
        }

        public IActionResult QuizzesView(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            ViewBag.SetName   = "LINUX REVIEWER";
            ViewBag.SetDate   = DateTime.Now.ToString("MMM dd, yyyy");
            ViewBag.QuizCount = 6;
            ViewBag.Quizzes   = null;
            ViewBag.ChatId    = id;
            ViewBag.Username  = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSession(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                foreach (var tbl in new[] { "files", "quizzes", "flashcards", "messages" })
                {
                    using var del = new NpgsqlCommand(
                        $"DELETE FROM {tbl} WHERE session_id = @sid", conn);
                    del.Parameters.AddWithValue("sid", id);
                    await del.ExecuteNonQueryAsync();
                }
                using var cmd = new NpgsqlCommand(
                    @"DELETE FROM chat_sessions s
                    USING chats c
                    WHERE s.session_id = @sid AND s.chat_id = c.chat_id AND c.user_id = @uid", conn);
                cmd.Parameters.AddWithValue("sid", id);
                cmd.Parameters.AddWithValue("uid", UserId);
                await cmd.ExecuteNonQueryAsync();
                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateChatRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO projects (user_id, name, subject, created_at)
                    VALUES (@uid, @name, @subject, @now)
                    RETURNING project_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("name", req.Name);
                cmd.Parameters.AddWithValue("subject", req.Subject ?? "");
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, name = req.Name, subject = req.Subject ?? "" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetMyProjects()
        {
            if (!IsLoggedIn()) return Unauthorized();
            var projects = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT p.project_id,
                            p.name,
                            COALESCE(p.subject, '') AS subject,
                            p.created_at,
                            COUNT(DISTINCT f.file_id) AS file_count
                    FROM projects p
                    LEFT JOIN chats c ON c.project_id = p.project_id
                    LEFT JOIN files f ON f.chat_id = c.chat_id
                    WHERE p.user_id = @uid
                    GROUP BY p.project_id
                    ORDER BY p.created_at DESC", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    projects.Add(new
                    {
                        id        = reader.GetInt32(0),
                        name      = reader.GetString(1),
                        subject   = reader.GetString(2),
                        date      = reader.GetDateTime(3).ToString("MMM dd, yyyy"),
                        fileCount = reader.GetInt64(4)
                    });
                }
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(projects);
        }

        /// <summary>
        /// POST /Chat/RenameProject
        /// Body: { "id": 42, "name": "New Name" }
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RenameProject([FromBody] RenameProjectRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name is required" });

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var cmd = new NpgsqlCommand(
                    @"UPDATE projects
                      SET name = @name
                      WHERE project_id = @pid AND user_id = @uid", conn);
                cmd.Parameters.AddWithValue("name", req.Name.Trim());
                cmd.Parameters.AddWithValue("pid",  req.Id);
                cmd.Parameters.AddWithValue("uid",  UserId);

                int rows = await cmd.ExecuteNonQueryAsync();
                if (rows == 0)
                    return NotFound(new { error = "Project not found or access denied." });

                return Json(new { success = true, name = req.Name.Trim() });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        /// <summary>
        /// DELETE /Chat/DeleteProject/{id}
        /// Cascade-deletes all chats, messages, files, quizzes, flashcards
        /// that belong to the project, then deletes the project itself.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteProject(int id)
        {
            if (!IsLoggedIn()) return Unauthorized();

            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                // ── 1. Verify ownership ───────────────────────────────────────
                using var check = new NpgsqlCommand(
                    "SELECT 1 FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                check.Parameters.AddWithValue("pid", id);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null)
                    return NotFound(new { error = "Project not found or access denied." });

                // ── 2. Get all chat IDs under this project ────────────────────
                var chatIds = new List<int>();
                using (var getChatIds = new NpgsqlCommand(
                    "SELECT chat_id FROM chats WHERE project_id = @pid", conn))
                {
                    getChatIds.Parameters.AddWithValue("pid", id);
                    using var reader = await getChatIds.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                        chatIds.Add(reader.GetInt32(0));
                }

                // ── 3. Delete all children for each chat ─────────────────────
                foreach (int cid in chatIds)
                {
                    foreach (var table in new[] { "flashcards", "quizzes", "files",
                                                  "chat_documents", "messages" })
                    {
                        using var del = new NpgsqlCommand(
                            $"DELETE FROM {table} WHERE chat_id = @cid", conn);
                        del.Parameters.AddWithValue("cid", cid);
                        await del.ExecuteNonQueryAsync();
                    }
                }

                // ── 4. Delete all chats under the project ────────────────────
                using (var delChats = new NpgsqlCommand(
                    "DELETE FROM chats WHERE project_id = @pid", conn))
                {
                    delChats.Parameters.AddWithValue("pid", id);
                    await delChats.ExecuteNonQueryAsync();
                }

                // ── 5. Delete the project itself ─────────────────────────────
                using (var delProj = new NpgsqlCommand(
                    "DELETE FROM projects WHERE project_id = @pid AND user_id = @uid", conn))
                {
                    delProj.Parameters.AddWithValue("pid", id);
                    delProj.Parameters.AddWithValue("uid", UserId);
                    await delProj.ExecuteNonQueryAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        [HttpGet]
        public async Task<IActionResult> GetChatsInProject(int projectId)
        {
            if (!IsLoggedIn()) return Unauthorized();
            var chats = new List<object>();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand(
                    @"SELECT c.chat_id, COALESCE(c.name, 'New Chat') AS title
                    FROM chats c
                    JOIN projects p ON p.project_id = c.project_id
                    WHERE c.project_id = @pid AND p.user_id = @uid
                    ORDER BY c.created_at DESC", conn);
                cmd.Parameters.AddWithValue("pid", projectId);
                cmd.Parameters.AddWithValue("uid", UserId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    chats.Add(new { id = reader.GetInt32(0), title = reader.GetString(1) });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
            return Json(chats);
        }

        [HttpPost]
        public async Task<IActionResult> NewChatInProject([FromBody] NewSessionRequest req)
        {
            if (!IsLoggedIn()) return Unauthorized();
            try
            {
                using var conn = RavnLearnWeb.Database.GetConnection();
                await conn.OpenAsync();

                using var check = new NpgsqlCommand(
                    "SELECT 1 FROM projects WHERE project_id = @pid AND user_id = @uid", conn);
                check.Parameters.AddWithValue("pid", req.FolderId);
                check.Parameters.AddWithValue("uid", UserId);
                if (await check.ExecuteScalarAsync() == null) return Forbid();

                using var cmd = new NpgsqlCommand(
                    @"INSERT INTO chats (user_id, project_id, name, created_at)
                    VALUES (@uid, @pid, 'New Chat', @now)
                    RETURNING chat_id", conn);
                cmd.Parameters.AddWithValue("uid", UserId);
                cmd.Parameters.AddWithValue("pid", req.FolderId);
                cmd.Parameters.AddWithValue("now", DateTime.UtcNow);
                int newId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                return Json(new { id = newId, title = "New Chat" });
            }
            catch (Exception ex) { return StatusCode(500, new { error = ex.Message }); }
        }

        public async Task<IActionResult> Open(int id)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");

            // Get the most recent chat in this project
            int lastChatId = -1;
            using var conn = RavnLearnWeb.Database.GetConnection();
            await conn.OpenAsync();
            using var cmd = new NpgsqlCommand(
                @"SELECT chat_id FROM chats 
                WHERE project_id = @pid AND user_id = @uid 
                ORDER BY created_at DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("pid", id);
            cmd.Parameters.AddWithValue("uid", UserId);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
                lastChatId = Convert.ToInt32(result);

            ViewBag.FolderId    = id;
            ViewBag.ChatId      = lastChatId;  // ← now passes the real chat ID
            ViewBag.Username    = Username;
            ViewBag.UserInitial = Username.Length > 0 ? Username[0].ToString().ToUpper() : "?";
            return View("Index");
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

    // ── Request models ────────────────────────────────────────────────────────

    public class SendMessageRequest
    {
        public int    ChatId  { get; set; }
        public string Message { get; set; } = "";
    }

    public class SaveQuizRequest
    {
        public int    ChatId   { get; set; }
        public string QuizText { get; set; } = "";
    }

    public class SaveQuizQuestionsRequest
    {
        public int                   ChatId    { get; set; }
        public List<QuizQuestionItem> Questions { get; set; } = new();
    }

    public class QuizQuestionItem
    {
        public string Question      { get; set; } = "";
        public string ChoiceA       { get; set; } = "";
        public string ChoiceB       { get; set; } = "";
        public string ChoiceC       { get; set; } = "";
        public string ChoiceD       { get; set; } = "";
        public string CorrectAnswer { get; set; } = "";
    }

    public class SaveFlashcardsRequest
    {
        public int               ChatId { get; set; }
        public List<FlashcardItem> Cards { get; set; } = new();
    }

    public class FlashcardItem
    {
        public string Front { get; set; } = "";
        public string Back  { get; set; } = "";
    }

    public class NewSessionRequest
    {
        public int FolderId { get; set; }
    }

    public class CreateChatRequest
    {
        public string Name    { get; set; } = "";
        public string Subject { get; set; } = "";
    }

    public class RenameProjectRequest
    {
        public int    Id   { get; set; }
        public string Name { get; set; } = "";
    }

    
}