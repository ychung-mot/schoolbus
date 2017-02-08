﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SchoolBus.WS.CCW.Facade.Service;
using SchoolBus.WS.CCW.Reference;
using System.ServiceModel;
using Microsoft.EntityFrameworkCore;
using CCW.Models;
using Microsoft.AspNetCore.Http;

namespace CCW.Controllers
{
    [Route("api/[controller]")]
    public class CCWController : Controller
    {
        private readonly IConfiguration Configuration;
        private readonly DbAppContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        ICCWService _service;

        private string userId;
        private string guid;
        private string directory;

        public CCWController(IHttpContextAccessor httpContextAccessor, IConfigurationRoot configuration, DbAppContext context)
        {
            Configuration = configuration;

            _service = CCWServiceFactory.CreateService(configuration);
            _httpContextAccessor = httpContextAccessor;
            _context = context;

            userId = getFromHeaders("SM_UNIVERSALID");
            guid = getFromHeaders("SMGOV_USERGUID");
            directory = getFromHeaders("SM_AUTHDIRNAME");
        }

        private string getFromHeaders(string key)
        {
            string result = null;
            if (Request.Headers.ContainsKey(key))
            {
                result = Request.Headers[key];
            }
            return result;
        }


        [HttpGet]
        [Route("GetBySerial/{serial}")]

        public virtual IActionResult GetBySerial([FromRoute]string serial)
        {
            var result = new ObjectResult("");
            try
            {
                result = new ObjectResult(_service.GetBCVehicleForSerialNumber(serial, userId, guid, directory));
            }
            catch (AggregateException ae)
            {
                ae.Handle((x) =>
                {
                    if (x is FaultException<CVSECommonException>) // From the web service.
                    {
                        result = new ObjectResult(x);
                        return true;
                    }
                    return true; // ignore other exceptions
                });
            }

            return new ObjectResult(result);            
        }

        [HttpGet]
        [Route("GetByRegi/{regi}")]
        public virtual IActionResult GetByRegi([FromRoute] string regi)
        {
            var result = new ObjectResult("");
            try
            {
                result = new ObjectResult(_service.GetBCVehicleForRegistrationNumber(regi, userId, guid, directory));                
            }
            catch (AggregateException ae)
            {
                ae.Handle((x) =>
                {
                    if (x is FaultException<CVSECommonException>) // From the web service.
                    {
                        result = new ObjectResult(x);
                        return true;
                    }
                    return true; // ignore other exceptions
                });
            }


            return new ObjectResult(result);          
        }

        [HttpGet]
        [Route("GetByPlate/{plate}")]
        public virtual IActionResult GetByPlate([FromRoute] string plate)
        {
            var result = new ObjectResult("");
            try
            {
                result = new ObjectResult(_service.GetBCVehicleForLicensePlateNumber(plate, userId, guid, directory));
            }
            catch (AggregateException ae)
            {
                ae.Handle((x) =>
                {
                    if (x is FaultException<CVSECommonException>) // From the web service.
                    {
                        result = new ObjectResult(x);
                        return true;
                    }
                    return true; // ignore other exceptions
                });
            }


            return new ObjectResult(result);
        }

        [HttpGet]
        [Route("GetCCW")]
        public virtual IActionResult GetCCW([FromQuery] string regi, [FromQuery] string plate, [FromQuery] string vin)
        {

            // check we have the right headers.
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(directory))
            {
                return new UnauthorizedResult();
            }

            // Check for the following data:
            // 1. registration
            // 2. plate
            // 3. decal

            VehicleDescription vehicle = null;
            if (regi != null)
            {
                try
                {
                    vehicle = _service.GetBCVehicleForRegistrationNumber(regi, userId, guid, directory);
                }
                catch (Exception e)
                {
                    vehicle = null;
                }
            }            
            if (vehicle == null && plate != null) // check the plate.
            {
                try
                {
                    vehicle = _service.GetBCVehicleForLicensePlateNumber(plate, userId, guid, directory);
                }
                catch (Exception e)
                {
                    vehicle = null;
                }
            }
            if (vehicle == null && vin != null) // check the vin.
            {
                try
                {
                    vehicle = _service.GetBCVehicleForSerialNumber(vin, userId, guid, directory);
                }
                catch (Exception e)
                {
                    vehicle = null;
                }
            }
            if (vehicle == null)
            {
                return new StatusCodeResult(404); // Can't find the vehicle.
            }
            else
            {
                string icbcRegistrationNumber = vehicle.registrationNumber;
                CCWData ccwdata = null;
                bool existing = false;
                if (_context.CCWDatas.Any(x => x.ICBCRegistrationNumber == icbcRegistrationNumber))
                {
                    ccwdata = _context.CCWDatas.First(x => x.ICBCRegistrationNumber == icbcRegistrationNumber);
                    existing = true;
                }
                else
                {
                    ccwdata = new CCWData();
                }

                // update the ccw record.

                ccwdata.ICBCBody = vehicle.bodyCode;
                ccwdata.ICBCColour = vehicle.colour;
                ccwdata.ICBCCVIPDecal = vehicle.inspectionDecalNumber;
                ccwdata.ICBCCVIPExpiry = vehicle.inspectionExpiryDate;
                ccwdata.ICBCFleetUnitNo = SanitizeInt(vehicle.fleetUnitNumber);
                ccwdata.ICBCFuel = vehicle.fuelTypeDescription;
                ccwdata.ICBCGrossVehicleWeight = SanitizeInt(vehicle.grossVehicleWeight);
                ccwdata.ICBCMake = vehicle.make;
                ccwdata.ICBCModel = vehicle.model;
                ccwdata.ICBCGrossVehicleWeight = SanitizeInt(vehicle.grossVehicleWeight);
                ccwdata.ICBCModelYear = SanitizeInt(vehicle.modelYear);
                ccwdata.ICBCNetWt = SanitizeInt(vehicle.netWeight);
                ccwdata.ICBCNotesAndOrders = vehicle.cvipDataFromLastInspection;
                ccwdata.ICBCOrderedOn = vehicle.firstOpenOrderDate;
                ccwdata.ICBCRateClass = vehicle.rateClass;
                ccwdata.ICBCRebuiltStatus = vehicle.statusCode;
                ccwdata.ICBCRegistrationNumber = vehicle.registrationNumber;
                ccwdata.ICBCRegOwnerAddr1 = vehicle.owner.mailingAddress1;
                ccwdata.ICBCRegOwnerAddr2 = vehicle.owner.mailingAddress2;
                ccwdata.ICBCRegOwnerCity = vehicle.owner.mailingAddress3;
                ccwdata.ICBCRegOwnerName = vehicle.owner.name1;
                ccwdata.ICBCRegOwnerPODL = vehicle.principalOperatorDlNum;
                ccwdata.ICBCRegOwnerPostalCode = vehicle.owner.postalCode;
                ccwdata.ICBCRegOwnerProv = vehicle.owner.mailingAddress4;
                ccwdata.ICBCRegOwnerRODL = vehicle.owner.driverLicenseNumber;
                ccwdata.ICBCRegOwnerStatus = "";
                ccwdata.ICBCSeatingCapacity = SanitizeInt(vehicle.seatingCapacity);
                ccwdata.ICBCVehicleType = vehicle.vehicleType;

                ccwdata.NSCPlateDecal = vehicle.decalNumber;
                ccwdata.NSCPolicyEffectiveDate = vehicle.policyStartDate;
                ccwdata.NSCPolicyExpiryDate = vehicle.policyExpiryDate;
                ccwdata.NSCPolicyStatus = vehicle.policyStatus;
                ccwdata.NSCPolicyStatusDate = vehicle.policyAcquiredCurrentStatusDate;
                

                // get the nsc client organization data.

                string nscnumber = vehicle.nscNumber;
                string organizationNameCode = vehicle.ownerNameCode;
                try
                {
                    ClientOrganization clientOrganization = _service.GetCurrentClientOrganization(nscnumber, organizationNameCode, userId, guid, directory);
                    ccwdata.NSCCarrierConditions = clientOrganization.nscInformation.carrierStatus;
                    ccwdata.NSCCarrierName = clientOrganization.name1;
                    ccwdata.NSCCarrierSafetyRating = clientOrganization.nscInformation.safetyRating;
                    ccwdata.NSCClientNum = clientOrganization.nscInformation.certificationNumber;                                        
                }
                catch (Exception e)
                {
                    
                }
                
                return new ObjectResult(ccwdata);
            }

        }

            /// <summary>
            /// Utility function to convert strings to nullable int.
            /// </summary>
            /// <param name="val"></param>
            /// <returns></returns>
            private int? SanitizeInt (string val)
            {
                int? result = null;
                try
                {
                    result = int.Parse(val);
                }
                catch (Exception e)
                {
                    result = null;
                }
                return result;
            }

        protected HttpRequest Request
        {
            get { return _httpContextAccessor.HttpContext.Request; }
        }
    }
    }