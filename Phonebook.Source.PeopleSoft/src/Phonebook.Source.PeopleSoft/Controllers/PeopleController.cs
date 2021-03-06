﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Oracle.EntityFrameworkCore.Storage.Internal;
using Phonebook.Source.PeopleSoft.Models;
using Phonebook.Source.PeopleSoft.Models.Context;

namespace Phonebook.Source.PeopleSoft.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PeopleController : ControllerBase
    {
        private readonly IMemoryCache memoryCache;

        public ModelContext Context { get; }

        public PeopleController(ModelContext context, IMemoryCache memoryCache)
        {
            Context = context;
            this.memoryCache = memoryCache;
        }
        // GET: api/People
        [HttpGet]
        public async Task<IEnumerable<Phonebook.Source.PeopleSoft.Models.Old.Person>> Get()
        {
            if (this.memoryCache.TryGetValue(this.GetType().Name, out IEnumerable<Phonebook.Source.PeopleSoft.Models.Old.Person> cachedPersons))
            {
                return cachedPersons;
            }
            var persons = (await this.InlcudeDependencies(Context.Peoples)).ToList().Select(d => new Phonebook.Source.PeopleSoft.Models.Old.Person(d));
            this.memoryCache.Set(this.GetType().Name, persons);
            return persons;
        }

        // GET: api/People/5
        [HttpGet("{id}")]
        public async Task<Person> Get(int id)
        {
            if (this.memoryCache.TryGetValue($"{this.GetType().Name}/{id}", out Person cachedPerson))
            {
                return cachedPerson;
            }
            var person = (await this.InlcudeDependencies(Context.Peoples)).First(p => p.Id == id);
            this.memoryCache.Set($"{this.GetType().Name}/{id}", person);
            return person;
        }

        private async Task<IEnumerable<Person>> InlcudeDependencies(IQueryable<Person> query)
        {
            return (
                    await query
                        .AsNoTracking()
                        .Include(p => p.Status)
                        .Include(p => p.Function)
                        .Include(p => p.OrgUnit)
                            .ThenInclude(o => o.Parent)
                        .Include(p => p.OrgUnit)
                            .ThenInclude(o => o.HeadOfOrgUnit)
                        .Include(p => p.OrgUnit)
                            .ThenInclude(o => o.OrgUnitToFunctions)
                                .ThenInclude(o => o.Person)
                        .Include(p => p.Room)
                            .ThenInclude(r => r.BuildingPart)
                                .ThenInclude(b => b.Building)
                                    .ThenInclude(b => b.Location)
                        .Include(p => p.Room)
                            .ThenInclude(r => r.Floor)
                                .ThenInclude(f => f.Building) // we must import also here the building because sometimes a room hasn't a buildingpart....
                                    .ThenInclude(b => b.Location)
                        .Include(p => p.Room)
                        .Where(p => p.ShortName != null)
                        .OrderBy(p => p.Id)
                        .ToListAsync()
                    )
                    // the following select will remove cicle references
                    .Select(p =>

                    new Person()
                    {
                        Id = p.Id,
                        EMail = p.EMail == null ? string.Empty : p.EMail,
                        FAX = p.FAX == null ? string.Empty : p.FAX,
                        FirstName = p.FirstName == null ? string.Empty : p.FirstName,
                        FunctionId = p.FunctionId,
                        Function = p.Function != null ? new Function() { Id = p.Function.Id, Label = p.Function.Label, Code = p.Function.Code } : null,
                        LastName = p.LastName == null ? string.Empty : p.LastName,
                        MobilPhone = p.MobilPhone == null ? string.Empty : p.MobilPhone,
                        OrgUnit = p.OrgUnit != null ? CreateOrgUnitTree(p.OrgUnit) : new OrgUnit(),
                        //OrgUnitId = p.OrgUnitId,
                        Phone = p.Phone == null ? string.Empty : p.Phone,
                        Room = p.Room != null ? CreateRoomTree(p.Room) : new Room(),
                        ShortName = p.ShortName == null ? string.Empty : p.ShortName,
                        StatusId = p.StatusId,
                        Status = p.Status != null ? new Status() { Id = p.Status.Id, Name = p.Status.Name, Code = p.Status.Code } : null,
                        Title = p.Title == null ? string.Empty : p.Title,
                        RoomId = p.RoomId
                    });

        }

        private Room CreateRoomTree(Room room)
        {
            var result = new Room();
            result.BuildingPart = room.BuildingPart != null ? new BuildingPart()
            {
                Description = room.BuildingPart.Description,
                Id = room.BuildingPart.Id,
                BuildingId = room.BuildingPart.BuildingId,
                Building = room.BuildingPart.Building != null ? new Building()
                {
                    Address = room.BuildingPart.Building.Address,
                    Id = room.BuildingPart.Building.Id,
                    Name = room.BuildingPart.Building.Name,
                    ShortName = room.BuildingPart.Building.ShortName,
                    Number = room.BuildingPart.Building.Number,
                    LocationId = room.BuildingPart.Building.LocationId,
                    Location = room.BuildingPart.Building.Location != null ? new Location()
                    {
                        ShortName = room.BuildingPart.Building.Location.ShortName,
                        Name = room.BuildingPart.Building.Location.Name,
                        Country = room.BuildingPart.Building.Location.Country,
                        Id = room.BuildingPart.Building.Location.Id
                    } : null
                } : new Building()
            } : new BuildingPart();
            result.Id = room.Id;
            result.FloorId = room.FloorId;
            result.Map = room.Map;
            result.Number = room.Number;
            result.Floor = room.Floor != null ? new Floor()
            {
                Id = room.Floor.Id,
                BuildingId = room.Floor.BuildingId,
                Number = room.Floor.Number,
                Building = room.Floor.Building != null ? new Building()
                {
                    Address = room.Floor.Building.Address,
                    Id = room.Floor.Building.Id,
                    Name = room.Floor.Building.Name,
                    ShortName = room.Floor.Building.ShortName,
                    Number = room.Floor.Building.Number,
                    LocationId = room.Floor.Building.LocationId,
                    Location = room.Floor.Building.Location != null ? new Location()
                    {
                        ShortName = room.Floor.Building.Location.ShortName,
                        Name = room.Floor.Building.Location.Name,
                        Country = room.Floor.Building.Location.Country,
                        Id = room.Floor.Building.Location.Id
                    } : null
                } : new Building(),
                Description = room.Floor.Description
            } : new Floor();

            return result;
        }

        private OrgUnit CreateOrgUnitTree(OrgUnit orgUnit)
        {
            var result = new OrgUnit();
            result.Id = orgUnit.Id;
            result.Name = orgUnit.Name;
            result.Parent = orgUnit.Parent != null ? CreateOrgUnitTree(orgUnit.Parent) : null;
            result.ParentId = orgUnit.ParentId;
            result.ShortName = orgUnit.ShortName;
            result.CostCenter = orgUnit.CostCenter;
            result.HeadOfOrgUnitId = orgUnit.HeadOfOrgUnitId;
            result.HeadOfOrgUnit = new Person()
            {
                ShortName = orgUnit.HeadOfOrgUnit == null ? string.Empty : orgUnit.HeadOfOrgUnit.ShortName,
                FirstName = orgUnit.HeadOfOrgUnit == null ? string.Empty : orgUnit.HeadOfOrgUnit.FirstName,
                LastName = orgUnit.HeadOfOrgUnit == null ? string.Empty : orgUnit.HeadOfOrgUnit.LastName

            };
            result.OrgUnitToFunctions = getOrgUnitFunction(orgUnit.OrgUnitToFunctions);

            return result;
        }

        private IEnumerable<OrgUnitToFunction> getOrgUnitFunction(IEnumerable<OrgUnitToFunction> orgUnitToFunctions)
        {
            foreach (var item in orgUnitToFunctions)
            {
                yield return new OrgUnitToFunction()
                {
                    FunctionId = item.FunctionId,
                    OrgUnitId = item.OrgUnitId,
                    PersonId = item.PersonId,
                    Person = new Person()
                    {
                        ShortName = item.Person.ShortName,
                        FirstName = item.Person.FirstName,
                        LastName = item.Person.LastName

                    },
                    RoleName = item.RoleName
                };
            }
        }
    }
}
