#pragma once

#include <Windows.h>

#include <algorithm>
#include <cstdint>
#include <string>
#include <unordered_map>
#include <vector>

/*
	A debug menu table's rows kept in a steady order. The menu's lists of things around the camera are nearest first, but
	sorted afresh every frame their rows swap places whenever two distances are close and the camera moves (and it always
	moves a little) - faster than they can be read. Nearest sorts a table's rows nearest first once a second and keeps that
	order in between, by an id that names each row from one frame to the next; rows that have appeared since go after the
	rest, nearest first, until the next sort. One order per table name; render thread (where the tables are drawn).
*/
namespace TABLE_ORDER
{
	constexpr ULONGLONG kResortMs = 1000;

	struct Order
	{
		std::vector<uint64_t> ids;
		ULONGLONG at = 0;
	};

	inline Order& OrderOf(const char* table)
	{
		static std::unordered_map<std::string, Order> orders;
		return orders[table];
	}

	// id(row): a number naming the row from frame to frame (its object's address, its uid); distance(row): how far away it is
	template <typename Row, typename Id, typename Distance>
	void Nearest(const char* table, std::vector<Row>& rows, Id id, Distance distance)
	{
		Order& order = OrderOf(table);
		const ULONGLONG now = GetTickCount64();
		if (order.ids.empty() || now - order.at >= kResortMs)
		{
			std::stable_sort(rows.begin(), rows.end(), [&](const Row& a, const Row& b)
			{
				const float da = distance(a), db = distance(b);
				return da != db ? da < db : id(a) < id(b);
			});
			order.ids.clear();
			for (const Row& row : rows)
				order.ids.push_back(id(row));
			order.at = now;
			return;
		}
		std::unordered_map<uint64_t, size_t> place;
		place.reserve(order.ids.size());
		for (size_t i = 0; i < order.ids.size(); i++)
			place.emplace(order.ids[i], i);
		auto rank = [&](const Row& row)
		{
			const auto it = place.find(id(row));
			return it == place.end() ? static_cast<size_t>(-1) : it->second;
		};
		std::stable_sort(rows.begin(), rows.end(), [&](const Row& a, const Row& b)
		{
			const size_t ra = rank(a), rb = rank(b);
			return ra != rb ? ra < rb : distance(a) < distance(b);
		});
	}
}
