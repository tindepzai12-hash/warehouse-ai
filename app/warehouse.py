class WarehouseAdapter:
    """Read-only synthetic context. Replace with authenticated WMS reads in B.

    No API client fields are copied into trusted facts. No stock quantities are
    exposed, including for blind counting. Refresh on every feedback request.
    """
    def facts(self,user,task_id):
        facts = {'synthetic':True,'taskId':task_id,'role':user['role'],
                 'inventoryTransactions':[],'approvedRoutes':[], 'approvedTargets':[],
                 'liveInventoryAvailable':False}
        if task_id=='TEST-PUT-001':
            facts.update(itemId='SKU-2048',assignedLocation='B-12-03',
                         locationAvailable=False,approvedAlternative=None)
        return facts
