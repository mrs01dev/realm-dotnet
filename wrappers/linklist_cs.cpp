/* Copyright 2015 Realm Inc - All Rights Reserved
 * Proprietary and Confidential
 */
 
#include <realm.hpp>
#include "error_handling.hpp"
#include "marshalling.hpp"
#include "realm_export_decls.hpp"
#include "shared_linklist.hpp"
#include "object-store/shared_realm.hpp"

using namespace realm;
using namespace realm::binding;

extern "C" {
  
REALM_EXPORT void linklist_add(SharedLinkViewRef* linklist_ptr, size_t row_ndx)
{
  handle_errors([&]() {
    (**linklist_ptr)->add(row_ndx);
  });
}

REALM_EXPORT void linklist_insert(SharedLinkViewRef* linklist_ptr, size_t link_ndx, size_t row_ndx)
{
  handle_errors([&]() {
    const size_t count = (**linklist_ptr)->size();
    if (link_ndx >= count)
      throw IndexOutOfRangeException("Insert into RealmList", link_ndx, count);
    (**linklist_ptr)->insert(link_ndx, row_ndx);
  });
}

REALM_EXPORT Row* linklist_get(SharedLinkViewRef* linklist_ptr, size_t link_ndx)
{
  return handle_errors([&]() -> Row* {
    const size_t count = (**linklist_ptr)->size();
    if (link_ndx >= count)
      throw IndexOutOfRangeException("Get from RealmList", link_ndx, count);
    auto rowExpr = (**linklist_ptr)->get(link_ndx);
    return new Row(rowExpr);
  });
}

REALM_EXPORT size_t linklist_find(SharedLinkViewRef* linklist_ptr, size_t row_ndx, size_t start_from)
{
  return handle_errors([&]() {
    return (**linklist_ptr)->find(row_ndx, start_from);
  });
}

REALM_EXPORT void linklist_erase(SharedLinkViewRef* linklist_ptr, size_t link_ndx)
{
  handle_errors([&]() {
    const size_t count = (**linklist_ptr)->size();
    if (link_ndx >= count)
      throw IndexOutOfRangeException("Erase item in RealmList", link_ndx, count);
    _impl::LinkListFriend::do_remove(***linklist_ptr,  link_ndx);
  });
}

REALM_EXPORT void linklist_clear(SharedLinkViewRef* linklist_ptr)
{
  handle_errors([&]() {
    (**linklist_ptr)->clear();
  });
}


REALM_EXPORT size_t linklist_size(SharedLinkViewRef* linklist_ptr)
{
    return handle_errors([&]() {
        return (**linklist_ptr)->size();
    });
}

  
REALM_EXPORT void linklist_destroy(SharedLinkViewRef* linklist_ptr)
{
  return handle_errors([&]() {
    delete linklist_ptr;
  });
}

}   // extern "C"
