using API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Npgsql;
using BCrypt;
using System.Drawing;
using System.Data;
using Newtonsoft.Json;
using Task = API.Models.Task;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class apiv1 : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public apiv1(IConfiguration config, IWebHostEnvironment webHostEnvironment)
        {
            _config = config;
            _webHostEnvironment = webHostEnvironment;
        }



        # region REGISTER API
        [HttpPost]
        [Produces("application/json")]
        [Route("register")]
        public IActionResult register([FromBody]registerUser user)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());

            var hashPassword = BCrypt.Net.BCrypt.HashPassword(user.password);

            NpgsqlCommand cmd = new NpgsqlCommand($"INSERT INTO users(name,age,dob,email,password) VALUES('{user.name}','{user.age}','{user.dob}','{user.email}','{hashPassword}') RETURNING id",con);
            try
            {
               con.Open();
               string res = cmd.ExecuteScalar().ToString();
               con.Close();
               var result = new { id = res };
               return Ok(result);
            }
            catch(Exception err)
            {
                con.Close();
                return BadRequest(new {status = false});
            }
        }

        #endregion

        #region LOGIN API
        [HttpPost]
        [Route("login")]
        public IActionResult login(loginUser user)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            NpgsqlCommand cmd = new NpgsqlCommand($"SELECT password FROM users WHERE email = '{user.email}'", con);
            con.Open();
            try
            {
                string hashPassword;
                var pass = cmd.ExecuteScalar();
                if (pass != null)
                {
                    hashPassword = pass.ToString();
                }
                else return Unauthorized(new {status = "Tài khoản hoặc mật khẩu sai!" });
                con.Close();
                bool verify = BCrypt.Net.BCrypt.Verify(user.password, hashPassword);
                if (verify) return Ok(new {status = true});
                else return Unauthorized(new { status = false });
            }catch
            {
                return Unauthorized(new { status = false });
            }
        }

        #endregion

        #region API avatar

        // Put avatar
        [HttpPut]
        [Route("avatar/{id}")]
        public async Task<IActionResult> UploadAvatar([FromRoute] Guid id,IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest();
            }
            else
            {
                using (var memoryStream = new MemoryStream())
                {
                    await file.CopyToAsync(memoryStream);
                    try
                    {
                        var img = Image.FromStream(memoryStream);
                        var byteImg = ImageToByteArray(img);
                        NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
                        NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE users SET avatar = @img WHERE id = '{id}'", con);
                        NpgsqlParameter parameter = new NpgsqlParameter("@img", NpgsqlTypes.NpgsqlDbType.Bytea);
                        parameter.Value = byteImg;
                        cmd.Parameters.Add(parameter);
                        con.Open();
                        try
                        {
                            cmd.ExecuteNonQuery();
                            con.Close();
                            return Ok(new { id = id });
                        }
                        catch
                        {
                            return BadRequest(new {status = false});
                        }

                    }
                    catch
                    {
                        return BadRequest(new { status = false });
                    }
                }
            }
        }


        //GET avatar
        [HttpGet]
        [Route("avatar/{id}")]
        public IActionResult getAvatar([FromRoute] Guid id)
        {
            try
            {
                NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
                NpgsqlCommand cmd = new NpgsqlCommand($"SELECT avatar FROM users WHERE id = '{id}'", con);
                con.Open();
                NpgsqlDataReader reader = cmd.ExecuteReader();
                byte[] byteImg = null;
                if (reader.Read())
                {
                    byteImg = (byte[])reader[0];
                }
                con.Close();
                return File(byteImg, "image/jpeg");
            }
            catch(Exception err)
            {
                return BadRequest(err.ToString());
            }
        }

        public byte[] ImageToByteArray(Image imageIn)
        {
            using (var ms = new MemoryStream())
            {
                imageIn.Save(ms, imageIn.RawFormat);
                return ms.ToArray();
            }
        }
        #endregion

        #region API task

        //POST task by userID
        [HttpPost]
        [Route("task/{id}")]
        public IActionResult postTask([FromRoute] Guid id, postTask task)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            NpgsqlCommand cmd = new NpgsqlCommand($"INSERT INTO tasks(user_id,task) VALUES('{id}','{task.task}') RETURNING id", con);
            con.Open();
            string taskID = cmd.ExecuteScalar().ToString();
            con.Close();
            return new JsonResult(new { id = taskID }) { StatusCode = StatusCodes.Status200OK };
        }

        //GET all task by userID
        [HttpGet]
        [Route("task/{id}")]
        public IActionResult getAllTaskByUserID([FromRoute] Guid id)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            NpgsqlCommand cmd = new NpgsqlCommand($"SELECT id,task,status,created_at,path_file FROM tasks WHERE user_id = '{id}'", con);
            DataTable table = new DataTable();
            con.Open();
            NpgsqlDataReader reader = cmd.ExecuteReader();
            if(reader.HasRows)
            {
                table.Load(reader);
                reader.Close();
                con.Close();
                string json = JsonConvert.SerializeObject(table);
                return Ok(json);
            }
            else
            {
                return BadRequest(new { status = false });
            }
        }

        //PUT task by taskID
        [HttpPut]
        [Route("task/{id}")]
        public IActionResult putTaskByTaskID([FromRoute] Guid id, Task task)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            try
            {
                if (task.status)
                {
                    if (task.path_file == "")
                    {
                        con.Open();
                        NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET task = '{task.task}',status = {task.status},updated_at = now(),finished_at = now() WHERE id = '{id}'", con);
                        cmd.ExecuteNonQuery();
                        con.Close();
                        return Ok(new { status = true });
                    }
                    else
                    {
                        con.Open();
                        NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET task = '{task.task}',status = {task.status},updated_at = now(),finished_at = now(),path_file = '{task.path_file}' WHERE id = '{id}'", con);
                        cmd.ExecuteNonQuery();
                        con.Close();
                        return Ok(new { status = true });
                    }
                }
                else
                {
                    if (task.path_file == "")
                    {
                        con.Open();
                        NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET task = '{task.task}',status = {task.status},updated_at = now() WHERE id = '{id}'", con);
                        cmd.ExecuteNonQuery();
                        con.Close();
                        return Ok(new { status = true });
                    }
                    else
                    {
                        con.Open();
                        NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET task = '{task.task}',status = {task.status},updated_at = now(),path_file = '{task.path_file}' WHERE id = '{id}'", con);
                        cmd.ExecuteNonQuery();
                        con.Close();
                        return Ok(new { status = true });
                    }
                }
            }
            catch
            {
                return BadRequest(new { status = false });
            }
        }



        #endregion

        #region API upload file
        [HttpPost]
        [Route("task/file/{id}")]
        public IActionResult uploadFile(IFormFile file, [FromRoute] Guid id)
        {
            string path = Path.Combine(_webHostEnvironment.ContentRootPath,"files/"+id.ToString());
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            string filePath = Path.Combine(path, file.FileName);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }
                NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET path_file = '{filePath.ToString()}' WHERE id = '{id}'", con);
                con.Open();
                cmd.ExecuteNonQuery();
                con.Close();
                return Ok(new { status = true});
            }
            else
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(path);
                foreach(FileInfo fileInfo in directoryInfo.GetFiles())
                {
                    fileInfo.Delete();
                }
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }
                NpgsqlCommand cmd = new NpgsqlCommand($"UPDATE tasks SET path_file = '{filePath.ToString()}' WHERE id = '{id}'", con);
                con.Open();
                cmd.ExecuteNonQuery();
                con.Close();
                return Ok(new { status = true });
            }
        }

        // GET file upload
        [HttpGet]
        [Route("task/file/{id}")]
        public IActionResult getFile([FromRoute] Guid id)
        {
            NpgsqlConnection con = new NpgsqlConnection(_config.GetConnectionString("todolist").ToString());
            NpgsqlCommand cmd = new NpgsqlCommand($"SELECT path_file FROM tasks WHERE id = '{id}'", con);
            con.Open();
            string path = cmd.ExecuteScalar().ToString();
            FileInfo file = new FileInfo(path);
            return File(System.IO.File.ReadAllBytes(path), "application/octet-stream", "file" + file.Extension);

        }
        #endregion 
    }
}
