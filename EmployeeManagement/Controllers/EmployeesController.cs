using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmployeeManagement.Data;
using EmployeeManagement.Models;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using Newtonsoft.Json;

namespace EmployeeManagement.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmployeesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        private readonly IDistributedCache _cache;

        public EmployeesController(ApplicationDbContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }


        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employee>>> GetEmployees()
        {
            var cacheKey = "employeeList";
            string serializedEmployeeList;
            var employeeList = new List<Employee>();
            var redisEmployeeList = await _cache.GetAsync(cacheKey);

            if (redisEmployeeList != null)
            {
                serializedEmployeeList = Encoding.UTF8.GetString(redisEmployeeList);
                employeeList = JsonConvert.DeserializeObject<List<Employee>>(serializedEmployeeList);
            }
            else
            {
                employeeList = await _context.Employees.ToListAsync();
                serializedEmployeeList = JsonConvert.SerializeObject(employeeList);
                redisEmployeeList = Encoding.UTF8.GetBytes(serializedEmployeeList);
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                await _cache.SetAsync(cacheKey, redisEmployeeList, options);
            }

            return Ok(employeeList);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<Employee>> GetEmployee(int id)
        {
            var cacheKey = $"employee_{id}";
            string serializedEmployee;
            var employee = new Employee();
            var redisEmployee = await _cache.GetAsync(cacheKey);

            if (redisEmployee != null)
            {
                serializedEmployee = Encoding.UTF8.GetString(redisEmployee);
                employee = JsonConvert.DeserializeObject<Employee>(serializedEmployee);
            }
            else
            {
                employee = await _context.Employees.FindAsync(id);

                if (employee == null)
                {
                    return NotFound();
                }

                serializedEmployee = JsonConvert.SerializeObject(employee);
                redisEmployee = Encoding.UTF8.GetBytes(serializedEmployee);
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                await _cache.SetAsync(cacheKey, redisEmployee, options);
            }

            return Ok(employee);
        }


        [HttpPost]
        public async Task<ActionResult<Employee>> PostEmployee(Employee employee)
        {
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEmployee", new { id = employee.Id }, employee);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutEmployee(int id, Employee employee)
        {
            if (id != employee.Id)
            {
                return BadRequest();
            }

            _context.Entry(employee).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _cache.Remove($"employee_{id}");
                // update the cache
                var cacheKey = $"employee_{id}";
                var serializedEmployee = JsonConvert.SerializeObject(employee);
                var redisEmployee = Encoding.UTF8.GetBytes(serializedEmployee);
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTime.Now.AddMinutes(5))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                await _cache.SetAsync(cacheKey, redisEmployee, options);

            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmployee(int id)
        {
            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employees.Any(e => e.Id == id);
        }
    }
}
